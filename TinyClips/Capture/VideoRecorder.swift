import ScreenCaptureKit
import AVFoundation
import CoreMedia
import CoreVideo

class VideoRecorder: NSObject, @unchecked Sendable {
    private var stream: SCStream?
    private var writer: AVAssetWriter?
    private var videoInput: AVAssetWriterInput?
    private var audioInput: AVAssetWriterInput?
    private var hasStartedWriting = false
    private var recordAudio = false
    private var outputURL: URL?
    private let writingQueue = DispatchQueue(label: "com.tinyclips.video-writing")

    func start(region: CaptureRegion, outputURL: URL) async throws {
        let filter = try await region.makeFilter()
        let config = region.makeStreamConfig()

        let settings = CaptureSettings.shared
        config.minimumFrameInterval = CMTime(value: 1, timescale: CMTimeScale(settings.videoFrameRate))
        config.showsCursor = true
        config.queueDepth = 8
        config.pixelFormat = kCVPixelFormatType_32BGRA

        self.recordAudio = settings.recordAudio
        if recordAudio {
            config.capturesAudio = true
            config.sampleRate = 48000
            config.channelCount = 2
        }

        self.outputURL = outputURL

        let pixelWidth = Int(region.sourceRect.width * region.scaleFactor)
        let pixelHeight = Int(region.sourceRect.height * region.scaleFactor)

        let writer = try AVAssetWriter(url: outputURL, fileType: .mp4)
        let videoInput = AVAssetWriterInput(mediaType: .video, outputSettings: [
            AVVideoCodecKey: AVVideoCodecType.h264,
            AVVideoWidthKey: pixelWidth,
            AVVideoHeightKey: pixelHeight,
        ])
        videoInput.expectsMediaDataInRealTime = true
        writer.add(videoInput)

        if recordAudio {
            let audioInput = AVAssetWriterInput(mediaType: .audio, outputSettings: [
                AVFormatIDKey: kAudioFormatMPEG4AAC,
                AVSampleRateKey: 48000,
                AVNumberOfChannelsKey: 2,
                AVEncoderBitRateKey: 128000,
            ])
            audioInput.expectsMediaDataInRealTime = true
            writer.add(audioInput)
            self.audioInput = audioInput
        }

        self.writer = writer
        self.videoInput = videoInput
        self.hasStartedWriting = false

        let stream = SCStream(filter: filter, configuration: config, delegate: nil)
        try stream.addStreamOutput(self, type: .screen, sampleHandlerQueue: writingQueue)
        if recordAudio {
            try stream.addStreamOutput(self, type: .audio, sampleHandlerQueue: writingQueue)
        }
        try await stream.startCapture()
        self.stream = stream
    }

    func stop() async throws -> URL {
        try await stream?.stopCapture()
        stream = nil

        guard let writer, let videoInput, let outputURL else {
            throw CaptureError.saveFailed
        }

        guard hasStartedWriting else {
            throw CaptureError.noFrames
        }

        let audioInput = self.audioInput

        return try await withCheckedThrowingContinuation { continuation in
            writingQueue.async {
                videoInput.markAsFinished()
                audioInput?.markAsFinished()
                writer.finishWriting {
                    if writer.status == .completed {
                        continuation.resume(returning: outputURL)
                    } else {
                        continuation.resume(throwing: writer.error ?? CaptureError.saveFailed)
                    }
                }
            }
        }
    }
}

extension VideoRecorder: SCStreamOutput {
    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard sampleBuffer.isValid else { return }
        guard let writer else { return }

        switch type {
        case .screen:
            // Only process frames with actual content
            guard let attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]],
                  let statusValue = attachments.first?[.status] as? Int,
                  let status = SCFrameStatus(rawValue: statusValue),
                  status == .complete else {
                return
            }

            guard let videoInput else { return }

            if !hasStartedWriting {
                guard writer.startWriting() else { return }
                writer.startSession(atSourceTime: sampleBuffer.presentationTimeStamp)
                hasStartedWriting = true
            }

            if videoInput.isReadyForMoreMediaData {
                videoInput.append(sampleBuffer)
            }

        case .audio:
            guard hasStartedWriting, let audioInput, audioInput.isReadyForMoreMediaData else { return }
            audioInput.append(sampleBuffer)

        @unknown default:
            break
        }
    }
}
