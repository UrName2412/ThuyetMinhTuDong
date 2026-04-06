# MainPage Refactoring Summary

## Overview
The `MainPage.xaml.cs` has been successfully refactored to separate concerns by extracting business logic into dedicated service classes. This improves testability, maintainability, and code organization.

## New Services Created

### 1. **TTSService** (`Services/TTSService.cs`)
Handles all Text-to-Speech related operations:
- **Initialization**: `InitializeAsync()` - Loads available locales and sets default language
- **Language Management**: 
  - `SetLanguage(languageCode)` - Change language
  - `GetLanguageDisplayName(languageCode)` - Get display name for a language
  - `GetVoicesForLanguage(languageCode)` - List voices for a language
  - `SetVoice(locale)` - Select specific voice
- **Speech Operations**:
  - `SpeakAsync(text)` - Speak text with automatic translation if needed
  - `StopSpeaking()` - Cancel TTS playback
- **Properties**:
  - `IsPlaying` - Get current playback state
  - `SelectedLanguageCode` - Get current language
  - `SelectedLocale` - Get current voice locale
  - `AvailableLocales` - Get all available locales
- **Events**:
  - `LanguageChanged` - Fires when language is changed
  - `PlayStarted` - Fires when TTS playback starts
  - `PlayStopped` - Fires when TTS playback stops
- **Internal Features**:
  - Automatic translation using Google Translate API for non-Vietnamese text
  - Cancellation token support for stopping playback
  - CultureInfo handling for language code normalization

### 2. **LocationService** (`Services/LocationService.cs`)
Manages location permissions and geolocation operations:
- **Permission Management**:
  - `CheckAndRequestPermissionAsync()` - Check and request location permission
- **Location Retrieval**:
  - `GetCurrentLocationAsync()` - Get user's current location (with fallback to last known)
  - `CreateMapSpan(location)` - Create a MapSpan for the given location (1km radius)
- **Events**:
  - `LocationObtained` - Fires when location is successfully obtained
  - `PermissionDenied` - Fires when permission is denied

### 3. **PlaceService** (`Services/PlaceService.cs`)
Manages Points of Interest (POI) database operations:
- **Data Retrieval**:
  - `GetAllPOIsAsync(forceRefresh)` - Get all POIs with optional cache refresh
- **Data Management**:
  - `SavePOIAsync(poi)` - Save a single POI
  - `SavePOIsAsync(pois)` - Save multiple POIs
  - `DeletePOIAsync(poi)` - Delete a POI
- **Initialization**:
  - `EnsureDefaultPOIsAsync(location)` - Create default sample POIs if database is empty
- **Utility**:
  - `CreateMapPin(poi)` - Convert POI to a Map Pin
  - `ClearCache()` - Force next read from database
- **Features**:
  - Built-in caching to reduce database calls
  - Automatic cache invalidation on data changes
  - Error handling with logging

## Refactored MainPage

The `MainPage.xaml.cs` has been simplified to:

### Removed Code
- Direct TTS implementation (`PlayTTSAsync`, `TranslateTextAsync`)
- Location permission and geolocation logic
- POI database operations (moved to service)
- TTS state management variables

### Maintained Code
- All UI event handlers
- Tab management
- Drawer expand/collapse animations
- Language parameter handling
- Place selection and display logic

### New Code
- Service initialization in constructor
- Service event subscriptions
- Delegation of operations to services

## Benefits

1. **Separation of Concerns**: Each service has a single responsibility
2. **Testability**: Services can be easily mocked for unit testing
3. **Reusability**: Services can be used in other pages
4. **Maintainability**: Easier to understand and modify specific functionality
5. **Error Handling**: Centralized error handling in services
6. **Caching**: Built-in caching in PlaceService reduces database calls

## Migration Guide

### For New Features
Instead of adding code to MainPage, create methods in appropriate services:
- TTS-related → `TTSService`
- Location-related → `LocationService`
- POI-related → `PlaceService`

### For Testing
You can now easily test individual services:
```csharp
var ttsService = new TTSService();
await ttsService.InitializeAsync();
var displayName = ttsService.GetLanguageDisplayName("vi");
```

### For Dependency Injection
Services can be registered in MauiProgram for dependency injection:
```csharp
builder.Services.AddSingleton<TTSService>();
builder.Services.AddSingleton<LocationService>();
builder.Services.AddSingleton(db => new PlaceService(db));
```

## No Breaking Changes
- All existing functionality is preserved
- UI behavior remains identical
- No changes required to XAML files
- No changes required to other pages/services

## Note
The warnings shown during build (ENC0004, ENC0085, ENC0033) are Edit and Continue (Hot Reload) warnings that appear because the application is being debugged with structural changes. These are not compilation errors. Simply restart the application or disable hot reload to apply changes while debugging.
