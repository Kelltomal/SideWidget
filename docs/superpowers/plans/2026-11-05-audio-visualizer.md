# Music reference redesign and real system-audio visualization

User: one music card resembling supplied large artwork/controls reference; sound-reactive wave resembling supplied spectrum image; wave directly on collapsed native header, brightness configurable. User explicitly chose all Windows output audio. Existing single-mode title copy, timing/seek, wheel volume, mixer, themes, timers and tooltip behavior remain.

Direction: Operate, reference-pinned. Larger square artwork left; artist/title/timeline/controls right; compact spacing. Real FFT wave behind native header text only while collapsed; matching low-contrast wave in music. Clock text stays above visualization. No fabricated animation, no saved PCM, no network audio transport.

Capture contract: SideWidget.Core.AudioSpectrumFrame(bool Available,string? Message,float[] Bands,double Rms,long Sequence), Bands 32 normalized finite 0..1. SideWidget.App.AudioSpectrumService has Latest immutable snapshot and SetEnabled(bool), Dispose. WASAPI render-default shared loopback, bounded FFT buffer, format-aware decoding, default-device recovery, bounded frame publication and silence reset. Capture disabled by persisted option stops stream. Capture worker never waits for WebView/UI.

Settings contract: VisualizerOptions(Enabled=true,ShowInHeader=true,Brightness=.35,Sensitivity=1,Style="wave"). Brightness0..1, sensitivity.25..4, style wave/bars. Nested Preferences.Visualizer optional for migration; defaults when missing. visualizer.settings applies/persists immediately; visualizer.get returns latest audio frame for web. Native accesses controller.Spectrum.Latest directly. Settings page and music menu share controls.

Tasks:
1. Capture/analysis agent: only new AudioSpectrum.cs, AudioSpectrumService.cs and new analyzer checks; synthetic sine/silence/format/disposal meaningful checks; no shared integration or GUI/publish.
2. Native renderer agent: only new MainWindow.Visualizer.cs. Methods InitializeAudioVisualizer(), UpdateAudioVisualizerGeometry(), RefreshAudioVisualizerSettings(), StopAudioVisualizer(). Reads controller.Spectrum and Settings.Visualizer; bounded dispatcher timer, XAML wave/bars, beneath clock/day/date, pointer transparent, reduced-motion/brightness/theme/disabled handling. No shared MainWindow edits or GUI/publish.
3. Main integration: settings/model/protocol/controller/UI, call renderer hooks in existing main, redesign one Music card and add web visualization/settings. Tests first for migration/bounds/protocol/settings updates and real frame data.
4. Verify C# + web + build; inspect dark/light music/settings at360/620/799, native collapsed visualizer with actual output audio and save/restart/disable/default-device handling where testable. Coordinate native exclusive slot via existing coordinator, install current complete native+Web candidate, check actual process/bundle. No git/package without coordinator.

Files reserved: capture agents own new files only; main owns small Preferences/Protocol/Controller/MainWindow integration and web main/types/WindowsPanels/music.css plus new web visualizer files. Do not overwrite unrelated changes. All work in existing shared workspace per ongoing user multi-chat coordination, no independent destructive checkout/worktree operations.
