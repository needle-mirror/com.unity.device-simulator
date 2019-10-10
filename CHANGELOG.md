# Changelog

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.2.0-preview] - 2019-10-11
### Added
- `Preferences -> Device Simulator` to set the customized devicee directory

## [1.1.0-preview] - 2019-09-26
### Added
- `Project Settings -> Device Simulator`
- SystemInfo simulation from arbitrary assemblies. Assembly list in Project Settings
- Spacer lines between foldouts in the control panel for visual clarity

### Changed
- `Use Player Settings` toggle was renamed to `Override Player Settings`. Its function was also inverted
- New device rotation icons

### Fixed
- Control panel now has a scroll view. UI elements no longer overlap each other when they don't fit
- UI elements from control panel no longer bleed into device panel
- Fixed "Layout update is struggling to process current layout (consider simplifying to avoid recursive layout)" error

## [1.0.0-preview] - 2019-09-23
### Added
- Simulating SystemInfo if called directly from Assembly-CSharp
- Warning is shown if simulator window is not active, which happens when some other simulator or game window owns rendering
- Simulating windowed mode on Android
- Simulating Screen.cutouts
- Highlight Safe Area toggle
- Documentation

### Changed
- Fit to Screen is now a toggle
- DeviceInfo format was changed to include multiple graphics APIs

## [0.1.0-preview.1] - 2019-08-21

### This is the first release of *Unity Device Simulator Package*.
