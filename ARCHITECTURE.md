# Architecture Overview

## Before Refactoring (Monolithic)

```
┌─────────────────────────────────────────────────────┐
│                    MainPage.xaml.cs                 │
│  (UI Logic + TTS + Location + Database Operations)  │
├─────────────────────────────────────────────────────┤
│                                                     │
│  • UI Event Handlers (Tabs, Swipes, Clicks)       │
│  • Language/Locale Management (TTS)                │
│  • Translation Logic (Google API)                  │
│  • Location Permissions                           │
│  • Geolocation Logic                              │
│  • POI Database Operations                        │
│  • Map Pin Creation                               │
│  • Text-to-Speech Playback                        │
│  • Animation & Drawer Logic                       │
│                                                     │
└─────────────────────────────────────────────────────┘
         ▲                      ▲
         │                      │
         ▼                      ▼
    LocalDatabase          TextToSpeech.Default
                           Geolocation.Default
```

## After Refactoring (Separation of Concerns)

```
┌──────────────────────────────────┐
│       MainPage.xaml.cs           │
│     (UI Logic & Orchestration)   │
├──────────────────────────────────┤
│ • Tab Management                 │
│ • Drawer Animations              │
│ • Language Navigation            │
│ • Place Selection Logic          │
│ • Service Coordination           │
└──────────────────────────────────┘
    ▲         ▲         ▲
    │         │         │
    ▼         ▼         ▼
┌──────────┐ ┌──────────────────┐ ┌─────────────┐
│TTSService│ │LocationService   │ │PlaceService │
├──────────┤ ├──────────────────┤ ├─────────────┤
│• Language│ │• Permissions     │ │• GetPOIs    │
│  Management │• Geolocation    │ │• SavePOI    │
│• Locale  │ │• MapSpan Create  │ │• DeletePOI  │
│  Selection│ │• Error Handling  │ │• Caching    │
│• TTS     │ │• Location Events │ │• MapPin     │
│  Playback│ │                  │ │  Conversion │
│• Text    │ └──────────────────┘ └─────────────┘
│  Translation│          ▲              ▲
│• Cancellation         │              │
└──────────┘     ┌──────┴──────┐      │
    ▲            ▼             │      ▼
    │   ┌─────────────────────┐   LocalDatabase
    │   │  External APIs      │
    └──►│  Locations Used:    │
        │  • TextToSpeech     │
        │  • Geolocation      │
        │  • Google Translate │
        └─────────────────────┘
```

## Data Flow Comparison

### Before (Tightly Coupled)
```
User Action
    │
    ▼
MainPage Event Handler
    │
    ├─► Direct API Call (TextToSpeech)
    ├─► Direct API Call (Geolocation)
    ├─► Direct DB Access (_database)
    ├─► Complex Business Logic
    ├─► State Management
    ├─► Error Handling
    └─► UI Updates
```

### After (Loosely Coupled)
```
User Action
    │
    ▼
MainPage Event Handler
    │
    ├─► TTSService.SpeakAsync()
    │   └─► Translation (internal)
    │   └─► TextToSpeech API (encapsulated)
    │   └─► Events: PlayStarted/PlayStopped
    │
    ├─► LocationService.GetCurrentLocationAsync()
    │   └─► Permissions (encapsulated)
    │   └─► Geolocation API (encapsulated)
    │   └─► Events: LocationObtained/PermissionDenied
    │
    ├─► PlaceService.GetAllPOIsAsync()
    │   └─► DB Access (encapsulated)
    │   └─► Caching Logic (internal)
    │   └─► MapPin Creation (encapsulated)
    │
    └─► UI Updates (from service events)
```

## Responsibility Matrix

| Concern | Before | After | Service |
|---------|--------|-------|---------|
| TTS Playback | MainPage | TTSService | ✓ |
| Language Selection | MainPage | TTSService | ✓ |
| Text Translation | MainPage | TTSService | ✓ |
| Location Permission | MainPage | LocationService | ✓ |
| Geolocation | MainPage | LocationService | ✓ |
| POI Database Ops | MainPage | PlaceService | ✓ |
| MapPin Creation | MainPage | PlaceService | ✓ |
| POI Caching | None | PlaceService | ✓ |
| UI Events | MainPage | MainPage | - |
| Tab Management | MainPage | MainPage | - |
| Drawer Animation | MainPage | MainPage | - |
| Error Handling | Mixed | Services | Consolidated |

## Dependency Graph

```
MainPage
├── Depends on: TTSService
│   └── Uses: TextToSpeech.Default, Google Translate API
│   └── No internal state (services manage it)
│
├── Depends on: LocationService
│   └── Uses: Permissions API, Geolocation.Default
│   └── No internal state (services manage it)
│
├── Depends on: PlaceService
│   └── Depends on: LocalDatabase
│   └── Uses: Internal Caching
│   └── No direct DB access from MainPage
│
└── No direct access to:
    ✗ TextToSpeech (via service only)
    ✗ Geolocation (via service only)
    ✗ Permissions (via service only)
    ✗ Database (via service only)
```

## Integration Points

### Service Events (Decoupled Communication)
```
TTSService                      MainPage
    │                               ▲
    └──► PlayStarted Event ─────────┤
    │                                │
    └──► PlayStopped Event ──────────┤
    │                                │
    └──► LanguageChanged Event ──────┘

LocationService                 MainPage
    │                               ▲
    └──► LocationObtained Event ────┤
    │                                │
    └──► PermissionDenied Event ────┘
```

## Benefits of Separation

```
┌────────────────────────────────────────────┐
│     Traditional Monolithic Approach         │
├────────────────────────────────────────────┤
│ ✗ Hard to test (MainPage has 10+ concerns) │
│ ✗ Hard to reuse (couple to MainPage)       │
│ ✗ Hard to maintain (logic scattered)       │
│ ✗ Hard to extend (ripple effects)          │
│ ✗ No caching/optimization opportunities    │
└────────────────────────────────────────────┘

┌────────────────────────────────────────────┐
│      Service-Based Approach (New)           │
├────────────────────────────────────────────┤
│ ✓ Easy to test (mock services)             │
│ ✓ Easy to reuse (use in other pages)       │
│ ✓ Easy to maintain (single responsibility) │
│ ✓ Easy to extend (isolated changes)        │
│ ✓ Built-in caching & optimization         │
│ ✓ Event-driven communication               │
│ ✓ Better error handling & logging          │
└────────────────────────────────────────────┘
```

## Testing Examples

### Before (Difficult)
```csharp
// Can't test MainPage without mocking entire TextToSpeech, 
// Geolocation, and database systems together
[Test]
public async Task CanPlayTTS() {
    // Impossible to test in isolation - 
    // MainPage tightly couples all these concerns
}
```

### After (Easy)
```csharp
// Each service can be tested independently
[Test]
public async Task TTSService_CanSpeak() {
    var service = new TTSService();
    await service.SpeakAsync("test");
    Assert.IsTrue(service.IsPlaying);
}

[Test]
public async Task PlaceService_CanGetPOIs() {
    var db = new LocalDatabase(testPath);
    var service = new PlaceService(db);
    var pois = await service.GetAllPOIsAsync();
    Assert.IsNotNull(pois);
}

// Integration test
[Test]
public async Task MainPage_CanSelectPlace() {
    var ttsService = new TTSService();
    var locationService = new LocationService();
    var placeService = new PlaceService(testDb);
    
    // All can be tested independently or together
}
```

## Future Improvements

```
Current State (v1.0)
    │
    ├─► DI Integration (register in MauiProgram)
    │
    ├─► Interface Abstraction (ITTSService, ILocationService)
    │
    ├─► Unit Test Suite (for each service)
    │
    ├─► Service Locator Pattern (optional)
    │
    └─► Analytics/Logging Integration
```

---

This architecture ensures **loose coupling, high cohesion, and maintainability** while preserving all existing functionality.
