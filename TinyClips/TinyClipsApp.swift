import SwiftUI

@main
struct TinyClipsApp: App {
    @StateObject private var captureManager = CaptureManager()
    @ObservedObject private var sparkleController = SparkleController.shared

    init() {
        _ = SparkleController.shared
    }

    var body: some Scene {
        MenuBarExtra("TinyClips", systemImage: captureManager.isRecording ? "record.circle.fill" : "camera.viewfinder") {
            MenuBarContentView(captureManager: captureManager, sparkleController: sparkleController)
        }

        Settings {
            SettingsView()
        }
    }
}

// MARK: - Menu Bar Content

private struct MenuBarContentView: View {
    @ObservedObject var captureManager: CaptureManager
    @ObservedObject var sparkleController: SparkleController
    @Environment(\.openSettings) private var openSettings
    @State private var isOptionPressed = false
    @State private var pollingTimer: Timer?

    var body: some View {
        if !captureManager.isRecording {
            Button(screenshotTitle) {
                captureManager.takeScreenshot(useFullScreen: isOptionPressed)
            }
            .keyboardShortcut("5", modifiers: [.command, .shift])

            Button(recordVideoTitle) {
                captureManager.startVideoRecording(useFullScreen: isOptionPressed)
            }
            .keyboardShortcut("6", modifiers: [.command, .shift])

            Button(recordGifTitle) {
                captureManager.startGifRecording(useFullScreen: isOptionPressed)
            }
            .keyboardShortcut("7", modifiers: [.command, .shift])

            Divider()
        } else {
            Button("Stop Recording") {
                captureManager.stopRecording()
            }
            .keyboardShortcut(".", modifiers: .command)

            Divider()
        }
#if !APPSTORE
        Button("Check for Updates\u{2026}") {
            sparkleController.checkForUpdates()
        }
#endif
        Button("Guide…") {
            captureManager.showGuide()
        }

        Button("Settings…") {
            openSettings()
            NSApp.activate()
        }
        .keyboardShortcut(",", modifiers: .command)

        Divider()

        Button("Quit") {
            NSApplication.shared.terminate(nil)
        }
        .keyboardShortcut("q", modifiers: .command)
        .onAppear {
            updateModifierState()
            let timer = Timer(timeInterval: 0.1, repeats: true) { _ in
                DispatchQueue.main.async {
                    updateModifierState()
                }
            }
            RunLoop.main.add(timer, forMode: .common)
            pollingTimer = timer
        }
        .onDisappear {
            pollingTimer?.invalidate()
            pollingTimer = nil
        }
    }

    private var screenshotTitle: String {
        isOptionPressed ? "Screenshot (Full Screen)" : "Screenshot"
    }

    private var recordVideoTitle: String {
        isOptionPressed ? "Record Video (Full Screen)" : "Record Video"
    }

    private var recordGifTitle: String {
        isOptionPressed ? "Record GIF (Full Screen)" : "Record GIF"
    }

    private func updateModifierState() {
        let hasOption = NSEvent.modifierFlags.contains(.option)
        if hasOption != isOptionPressed {
            isOptionPressed = hasOption
        }
    }
}

@MainActor
class CaptureManager: ObservableObject {
    @Published var isRecording = false

    private var videoRecorder: VideoRecorder?
    private var gifWriter: GifWriter?
    private var startPanel: StartRecordingPanel?
    private var stopPanel: StopRecordingPanel?
    private var pendingVideoRegion: CaptureRegion?
    private var trimmerWindow: VideoTrimmerWindow?
    private var gifTrimmerWindow: GifTrimmerWindow?
    private var screenshotEditorWindow: ScreenshotEditorWindow?
    private var countdownWindow: CountdownWindow?
    private var onboardingWindow: OnboardingWizardWindow?
    private var guideWindow: GuideWindow?

    init() {
        DispatchQueue.main.async { [weak self] in
            self?.showOnboardingIfNeeded()
        }
    }

    func takeScreenshot(useFullScreen: Bool = false) {
        Task {
            guard await PermissionManager.shared.checkPermission() else { return }
            guard let region = await chooseCaptureRegion(useFullScreen: useFullScreen) else { return }

            do {
                let url = try await ScreenshotCapture.capture(region: region)
                if CaptureSettings.shared.showScreenshotEditor {
                    showScreenshotEditor(for: url)
                } else {
                    SaveService.shared.handleSavedFile(url: url, type: .screenshot)
                }
            } catch {
                SaveService.shared.showError("Screenshot failed: \(error.localizedDescription)")
            }
        }
    }

    func startVideoRecording(useFullScreen: Bool = false) {
        Task {
            guard await PermissionManager.shared.checkPermission() else { return }
            guard let region = await chooseCaptureRegion(useFullScreen: useFullScreen) else { return }

            self.pendingVideoRegion = region
            showStartPanel()
        }
    }

    private func beginVideoRecording(region: CaptureRegion, systemAudio: Bool, microphone: Bool) {
        let settings = CaptureSettings.shared
        settings.recordAudio = systemAudio
        settings.recordMicrophone = microphone

        let doRecord = { [weak self] in
            guard let self else { return }
            Task {
                let url = SaveService.shared.generateURL(for: .video)

                do {
                    let recorder = VideoRecorder()
                    self.videoRecorder = recorder
                    self.isRecording = true

                    try await recorder.start(region: region, outputURL: url)
                    self.showStopPanel()
                } catch {
                    self.isRecording = false
                    SaveService.shared.showError("Video recording failed: \(error.localizedDescription)")
                }
            }
        }

        showCountdownThen(for: .video, action: doRecord)
    }

    func startGifRecording(useFullScreen: Bool = false) {
        Task {
            guard await PermissionManager.shared.checkPermission() else { return }
            guard let region = await chooseCaptureRegion(useFullScreen: useFullScreen) else { return }

            let doRecord = { [weak self] in
                guard let self else { return }
                Task {
                    do {
                        let writer = GifWriter()
                        self.gifWriter = writer
                        self.isRecording = true

                        try await writer.start(region: region)
                        self.showStopPanel()
                    } catch {
                        self.isRecording = false
                        SaveService.shared.showError("GIF recording failed: \(error.localizedDescription)")
                    }
                }
            }

            showCountdownThen(for: .gif, action: doRecord)
        }
    }

    func stopRecording() {
        Task {

            var savedVideoURL: URL?

            if let recorder = videoRecorder {
                do {
                    savedVideoURL = try await recorder.stop()
                } catch {
                    SaveService.shared.showError("Video save failed: \(error.localizedDescription)")
                }
                videoRecorder = nil
            }

            if let writer = gifWriter {
                let url = SaveService.shared.generateURL(for: .gif)
                do {
                    if CaptureSettings.shared.showGifTrimmer {
                        let gifData = try await writer.stopAndReturnData()
                        showGifTrimmer(gifData: gifData, outputURL: url)
                    } else {
                        try await writer.stop(outputURL: url)
                        SaveService.shared.handleSavedFile(url: url, type: .gif)
                    }
                } catch {
                    SaveService.shared.showError("GIF save failed: \(error.localizedDescription)")
                }
                gifWriter = nil
            }

            isRecording = false
            dismissStopPanel()

            // Show editor windows AFTER all recording resources are released
            // and UI state is cleaned up, so AVPlayer doesn't contend with
            // AVAssetWriter for the same file.
            if let savedVideoURL {
                if CaptureSettings.shared.showTrimmer {
                    showTrimmer(for: savedVideoURL)
                } else {
                    SaveService.shared.handleSavedFile(url: savedVideoURL, type: .video)
                }
            }
        }
    }

    private func showScreenshotEditor(for url: URL) {
        let window = ScreenshotEditorWindow(imageURL: url) { [weak self] resultURL in
            guard let self else { return }
            if let resultURL {
                SaveService.shared.handleSavedFile(url: resultURL, type: .screenshot)
            } else {
                try? FileManager.default.removeItem(at: url)
            }
            DispatchQueue.main.async {
                self.screenshotEditorWindow = nil
            }
        }
        self.screenshotEditorWindow = window
        DispatchQueue.main.async {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate()
        }
    }

    private func showTrimmer(for url: URL) {
        let window = VideoTrimmerWindow(videoURL: url) { [weak self] resultURL in
            guard let self else { return }
            if let resultURL {
                SaveService.shared.handleSavedFile(url: resultURL, type: .video)
            } else {
                // User cancelled — clean up the raw file
                try? FileManager.default.removeItem(at: url)
            }
            // Defer release so the window isn't deallocated mid-callback
            DispatchQueue.main.async {
                self.trimmerWindow = nil
            }
        }
        self.trimmerWindow = window
        // Defer showing to next run loop to avoid issues with menu tracking
        DispatchQueue.main.async {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate()
        }
    }

    private func showGifTrimmer(gifData: GifCaptureData, outputURL: URL) {
        let window = GifTrimmerWindow(gifData: gifData, outputURL: outputURL) { [weak self] resultURL in
            guard let self else { return }
            if let resultURL {
                SaveService.shared.handleSavedFile(url: resultURL, type: .gif)
            }
            DispatchQueue.main.async {
                self.gifTrimmerWindow = nil
            }
        }
        self.gifTrimmerWindow = window
        DispatchQueue.main.async {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate()
        }
    }

    private func showStartPanel() {
        let panel = StartRecordingPanel(
            onStart: { [weak self] systemAudio, mic in
                guard let self, let region = self.pendingVideoRegion else { return }
                self.pendingVideoRegion = nil
                self.dismissStartPanel()
                self.beginVideoRecording(region: region, systemAudio: systemAudio, microphone: mic)
            },
            onCancel: { [weak self] in
                self?.pendingVideoRegion = nil
                self?.dismissStartPanel()
            }
        )
        panel.show()
        self.startPanel = panel
    }

    private func dismissStartPanel() {
        startPanel?.dismiss()
        startPanel = nil
    }

    private func showStopPanel() {
        let panel = StopRecordingPanel { [weak self] in
            self?.stopRecording()
        }
        panel.show()
        self.stopPanel = panel
    }

    private func dismissStopPanel() {
        stopPanel?.close()
        stopPanel = nil
    }

    private func showCountdownThen(for type: CaptureType, action: @escaping () -> Void) {
        let settings = CaptureSettings.shared
        let enabled: Bool
        let duration: Int

        switch type {
        case .video:
            enabled = settings.videoCountdownEnabled
            duration = settings.videoCountdownDuration
        case .gif:
            enabled = settings.gifCountdownEnabled
            duration = settings.gifCountdownDuration
        default:
            action()
            return
        }

        guard enabled else {
            action()
            return
        }
        let window = CountdownWindow(duration: duration) {
            action()
        }
        self.countdownWindow = window
        window.show()
    }

    private func showOnboardingIfNeeded() {
        let settings = CaptureSettings.shared
        guard !settings.hasCompletedOnboarding, onboardingWindow == nil else { return }

        let window = OnboardingWizardWindow { [weak self] completed in
            if completed {
                settings.hasCompletedOnboarding = true
            }
            DispatchQueue.main.async {
                self?.onboardingWindow = nil
            }
        }
        onboardingWindow = window

        DispatchQueue.main.async {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate()
        }
    }

    func showGuide() {
        if let guideWindow {
            DispatchQueue.main.async {
                guideWindow.makeKeyAndOrderFront(nil)
                NSApp.activate()
            }
            return
        }

        let window = GuideWindow(onDismiss: { [weak self] in
            DispatchQueue.main.async {
                self?.guideWindow = nil
            }
        })

        self.guideWindow = window
        DispatchQueue.main.async {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate()
        }
    }

    private func chooseCaptureRegion(useFullScreen: Bool) async -> CaptureRegion? {
        if useFullScreen {
            guard let screen = screenUnderMouseCursor() ?? NSScreen.main else {
                return nil
            }
            return CaptureRegion.fullScreen(for: screen)
        }

        return await RegionSelector.selectRegion()
    }

    private func screenUnderMouseCursor() -> NSScreen? {
        let mouseLocation = NSEvent.mouseLocation
        return NSScreen.screens.first(where: { $0.frame.contains(mouseLocation) })
    }
}
