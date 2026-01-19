# Neonalig Collections

Custom collection types for Unity with enhanced functionality.

## Features

- **EnumDictionary**: Type-safe dictionary using enum keys with default value support
- **PriorityList**: List that maintains items sorted by priority

## Usage

### EnumDictionary

```csharp
using Neonalig.Collections;

public enum GameState { Menu, Playing, Paused, GameOver }

[Serializable]
public class MyComponent : MonoBehaviour
{
    public EnumDictionary<GameState, float> stateTimeouts;
    
    void Start()
    {
        // Access with enum keys
        float timeout = stateTimeouts[GameState.Playing];
    }
}
```

### PriorityList

```csharp
using Neonalig.Collections;

var list = new PriorityList<MyItem>();
list.Add(item, priority: 10);
// Items are automatically sorted by priority
```

## Installation

```json
{
  "dependencies": {
    "com.neonalig.collections": "1.0.0",
    "com.neonalig.polyfills": "1.0.0"
  }
}
```
