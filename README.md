# Neonalig Collections

Strongly-typed, Unity-friendly collection types with additional guarantees and ergonomics.

## Features

* **EnumDictionary**
  Dictionary keyed by enums with serialized support and default values
* **PriorityList**
  Automatically sorted list by priority

## Usage

### EnumDictionary

```csharp
using Neonalig.Collections;
using UnityEngine;

public enum GameState { Menu, Playing, Paused }

public class MyComponent : MonoBehaviour
{
    public EnumDictionary<GameState, float> stateTimeouts;

    void Start()
    {
        float timeout = stateTimeouts[GameState.Playing];
    }
}
```

### PriorityList

```csharp
using Neonalig.Collections;

var list = new PriorityList<MyItem>();
list.Add(item, priority: 10);
```

## Dependencies

This package is dependent on [com.neonalig.polyfills](https://github.com/Neonalig/com.neonalig.polyfills).

Ensure you have installed that package prior to this one, or your project will not be able to be compiled.

## Installation

### Option 1 - Package Manager (Recommended)

1. Open **Window ▸ Package Manager**
2. Click **➕**
3. Select **Install package from Git URL…**
4. Paste:

```
https://github.com/Neonalig/com.neonalig.collections.git#v1.0.0
```

Supported suffixes:

* `#v1.0.0` – tag
* `#main` – branch
* `#<commit-hash>` – exact commit

> **Tip:** Using a tag or commit hash is recommended for reproducible builds.

---

### Option 2 - `manifest.json`

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.neonalig.attributes": "https://github.com/Neonalig/com.neonalig.collections.git#v1.0.0"
  }
}
```

---

### Option 3 - Scoped Dependency

If you are consuming this from a local package or a scoped registry, use the package name directly:

```json
{
  "dependencies": {
    "com.neonalig.collections": "1.0.0"
  }
}
```

### Requirements

* Unity **2021.3 LTS** or newer