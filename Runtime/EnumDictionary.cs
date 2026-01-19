using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Neonalig.Collections
{
    /// <summary>
    /// Base class for the <see cref="EnumDictionary{TEnum,TValue}"/> implementation.<br/>
    /// An empty base class is required to allow Unity's editor system to wrap all strongly-typed dictionaries in a single common editor.
    /// </summary>
    [Obsolete("Did you mean EnumDictionary<TEnum, TValue>? This class is only used to allow Unity's editor system to wrap all strongly-typed dictionaries in a single common editor. It is not intended for direct use.")]
    public abstract class EnumDictionary
    {
    }

    /// <summary>
    /// A strongly-typed dictionary that maps enum values to values of type <typeparamref name="TValue"/>.<br/>
    /// It provides a way to store and retrieve values based on enum keys, with support for default values and serialization.<br/>
    /// If an 'override' is not defined for a specific enum value, the <see cref="DefaultValue"/> is returned instead (ensuring the dictionary always has a value for every enum key, without bloating memory usage).<br/>
    /// </summary>
    /// <typeparam name="TEnum">The enum type to use as keys in the dictionary. Must be a valid enum type.</typeparam>
    /// <typeparam name="TValue">The type of values to store in the dictionary. Must be a non-nullable type.</typeparam>
    [Serializable]
    public class EnumDictionary<TEnum, TValue> :
 #pragma warning disable CS0618 // Type or member is obsolete // Justification: Obsoletion warning exists just to warn developers not to use the base class directly
        EnumDictionary,
 #pragma warning restore CS0618 // Type or member is obsolete
        IReadOnlyDictionary<TEnum, TValue>,
        IDictionary<TEnum, TValue>,
        ISerializationCallbackReceiver
        where TEnum : struct, Enum
        where TValue : notnull
    {

        #region Dictionary Implementation

        private readonly Dictionary<TEnum, TValue> _dictionary;
        private readonly IEqualityComparer<TValue> _valueComparer;
        [SerializeField] private TValue _defaultValue;
        public TValue DefaultValue
        {
            get => _defaultValue;
            set
            {
                if (_valueComparer.Equals(_defaultValue, value)) return; // Avoid unnecessary updates
                _defaultValue = value;
                _version++;
                // TODO: Simplification pass? (add entries with the old default, remove entries with the new default)
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumDictionary{TEnum, TValue}"/> class with a specified default value, capacity, and optional comparers.<br/>
        /// This constructor allows you to define a default value that will be returned for enum keys that do not have an override defined in the dictionary.<br/>
        /// The dictionary will be initialized with the specified capacity and comparers for keys and values.<br/>
        /// </summary>
        /// <remarks>If you are unsure about the expected number of entries, you can use the default constructor which initializes the dictionary with a capacity equal to the number of defined enum values.</remarks>
        /// <param name="defaultValue">The default value to return for enum keys that do not have an override defined in the dictionary.</param>
        /// <param name="capacity">The initial capacity of the dictionary. This can help optimize performance if you know the expected number of override entries.</param>
        /// <param name="keyComparer">The comparer to use for comparing enum keys. If null, the default equality comparer for the enum type will be used.</param>
        /// <param name="valueComparer">The comparer to use for comparing values. If null, the default equality comparer for the value type will be used.</param>
        public EnumDictionary(TValue defaultValue = default!, int capacity = 0, IEqualityComparer<TEnum>? keyComparer = null, IEqualityComparer<TValue>? valueComparer = null)
        {
            _dictionary = new Dictionary<TEnum, TValue>(capacity, keyComparer ?? EqualityComparer<TEnum>.Default);
            _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
            _defaultValue = defaultValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumDictionary{TEnum, TValue}"/> class with a specified default value and optional comparers.
        /// </summary>
        /// <param name="defaultValue">The default value to return for enum keys that do not have an override defined in the dictionary.</param>
        /// <param name="keyComparer">The comparer to use for comparing enum keys. If null, the default equality comparer for the enum type will be used.</param>
        /// <param name="valueComparer">The comparer to use for comparing values. If null, the default equality comparer for the value type will be used.</param>
        public EnumDictionary(TValue defaultValue = default!, IEqualityComparer<TEnum>? keyComparer = null, IEqualityComparer<TValue>? valueComparer = null)
            : this(defaultValue, Enum.GetValues(typeof(TEnum)).Length, keyComparer, valueComparer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumDictionary{TEnum, TValue}"/> class.
        /// </summary>
        public EnumDictionary() : this(default(TValue)!, Enum.GetValues(typeof(TEnum)).Length, null, null) { }

        private int _version; // Used to track modifications to the dictionary for enumerator validity (i.e., to prevent modification during enumeration).

        struct Enumerator : IEnumerator<KeyValuePair<TEnum, TValue>>
        {
            private readonly EnumDictionary<TEnum, TValue> _enumDict;
            private int _index;
            private readonly int _version;

            public Enumerator(EnumDictionary<TEnum, TValue> enumDict)
            {
                _enumDict = enumDict;
                _index = -1;
                Current = default(KeyValuePair<TEnum, TValue>);
                _version = enumDict._version;
            }

            public KeyValuePair<TEnum, TValue> Current { get; private set; }
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (_version != _enumDict._version)
                {
                    throw new InvalidOperationException("The collection was modified after the enumerator was created.");
                }
                _index++;
                Array enumValues = Enum.GetValues(typeof(TEnum));
                if (_index >= enumValues.Length)
                {
                    return false;
                }
                TEnum key = (TEnum)enumValues.GetValue(_index)!;
                Current = _enumDict._dictionary.TryGetValue(key, out TValue value)
                    ? new KeyValuePair<TEnum, TValue>(key, value)
                    : new KeyValuePair<TEnum, TValue>(key, _enumDict.DefaultValue);
                return true;
            }

            public void Reset()
            {
                _index = -1;
                Current = default(KeyValuePair<TEnum, TValue>);
            }

            public void Dispose()
            {
                // No resources to dispose
            }
        }

        /// <inheritdoc />
        /// <remarks>This enumerator iterates through all enum values, returning the value from the dictionary if it exists, or the <see cref="DefaultValue"/> otherwise.<br/>
        /// For only iterating through existing keys, use <see cref="GetDefinedEnumerator"/> instead.</remarks>
        public IEnumerator<KeyValuePair<TEnum, TValue>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Gets an enumerator that iterates through the dictionary, returning only existing keys and their values.<br/>
        /// This is useful for iterating through the dictionary without the default values.
        /// </summary>
        public IEnumerator<KeyValuePair<TEnum, TValue>> GetDefinedEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        void ICollection<KeyValuePair<TEnum, TValue>>.Add(KeyValuePair<TEnum, TValue> item) => Add(item.Key, item.Value);

        /// <inheritdoc />
        public void Clear()
        {
            _dictionary.Clear();
            _version++;
        }

        /// <inheritdoc />
        bool ICollection<KeyValuePair<TEnum, TValue>>.Contains(KeyValuePair<TEnum, TValue> item) =>
            ContainsKey(item.Key) && _valueComparer.Equals(this[item.Key], item.Value);

        /// <inheritdoc />
        void ICollection<KeyValuePair<TEnum, TValue>>.CopyTo(KeyValuePair<TEnum, TValue>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex + Count > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            foreach (KeyValuePair<TEnum, TValue> kvp in this)
            {
                array[arrayIndex++] = kvp;
            }
        }

        /// <inheritdoc />
        bool ICollection<KeyValuePair<TEnum, TValue>>.Remove(KeyValuePair<TEnum, TValue> item) =>
            ContainsKey(item.Key) && _valueComparer.Equals(this[item.Key], item.Value) && Remove(item.Key);

        /// <inheritdoc />
        int ICollection<KeyValuePair<TEnum, TValue>>.Count => Count;

        /// <inheritdoc />
        bool ICollection<KeyValuePair<TEnum, TValue>>.IsReadOnly => ((ICollection<KeyValuePair<TEnum, TValue>>)_dictionary).IsReadOnly;

        /// <inheritdoc />
        public int Count => _dictionary.Count;

        /// <inheritdoc />
        public void Add(TEnum key, TValue value)
        {
            if (!_dictionary.TryAdd(key, value))
            {
                throw new ArgumentException($"Key {key} already exists in the dictionary.");
            }
            _version++;
        }

        /// <inheritdoc />
        bool IDictionary<TEnum, TValue>.ContainsKey(TEnum key) => ContainsKey(key);

        /// <inheritdoc />
        public bool Remove(TEnum key)
        {
            if (_dictionary.Remove(key))
            {
                _version++;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        bool IDictionary<TEnum, TValue>.TryGetValue(TEnum key, out TValue value) => TryGetValue(key, out value);

        /// <inheritdoc />
        /// <remarks>This method checks if the key is defined in the enum type, but does not check if it exists in the dictionary (as <see cref="DefaultValue"/> is returned for undefined keys).<br/>
        /// If you want to check if the key exists in the dictionary, use <see cref="ContainsDefinedKey(TEnum)"/> instead.</remarks>
        public bool ContainsKey(TEnum key) => Enum.IsDefined(typeof(TEnum), key);

        /// <summary>Determines whether the dictionary contains a defined key.</summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key is defined in the enum and exists in the dictionary; otherwise, false.</returns>
        public bool ContainsDefinedKey(TEnum key)
        {
            if (!Enum.IsDefined(typeof(TEnum), key))
            {
                return false;
            }
            return _dictionary.ContainsKey(key);
        }

        /// <inheritdoc />
        /// <remarks>This method returns the value from the dictionary if it exists, or the <see cref="DefaultValue"/> otherwise.<br/>
        /// If you want to check if the key exists in the dictionary only, use <see cref="TryGetDefinedValue(TEnum,out TValue)"/> instead.</remarks>
        public bool TryGetValue(TEnum key, out TValue value)
        {
            if (_dictionary.TryGetValue(key, out value))
            {
                return true;
            }
            value = DefaultValue;
            return true;
        }

        /// <summary>Attempts to get the value associated with the specified key, returning true if it exists in the dictionary.</summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, or the <see cref="DefaultValue"/> if the key is not defined in the enum.</param>
        /// <returns>True if the key is defined in the enum and exists in the dictionary; otherwise, false.</returns>
        public bool TryGetDefinedValue(TEnum key, [MaybeNullWhen(false)] out TValue value)
        {
            if (!Enum.IsDefined(typeof(TEnum), key))
            {
                value = DefaultValue;
                return false;
            }
            return _dictionary.TryGetValue(key, out value);
        }

        /// <inheritdoc cref="IDictionary{TEnum,TValue}.this"/>
        public TValue this[TEnum key]
        {
            get => _dictionary.GetValueOrDefault(key, DefaultValue);
            set => _dictionary[key] = value;
        }

        /// <inheritdoc />
        IEnumerable<TEnum> IReadOnlyDictionary<TEnum, TValue>.Keys => Keys;

        /// <inheritdoc />
        IEnumerable<TValue> IReadOnlyDictionary<TEnum, TValue>.Values => Values;

        /// <inheritdoc />
        ICollection<TEnum> IDictionary<TEnum, TValue>.Keys => _dictionary.Keys;

        /// <inheritdoc />
        ICollection<TValue> IDictionary<TEnum, TValue>.Values => _dictionary.Values;

        /// <inheritdoc cref="IReadOnlyDictionary{TEnum,TValue}.Keys"/>
        public IReadOnlyCollection<TEnum> Keys => _dictionary.Keys;

        /// <inheritdoc cref="IReadOnlyDictionary{TEnum,TValue}.Values"/>
        public IReadOnlyCollection<TValue> Values => _dictionary.Values;

        #endregion

        #region Serialization

        [SerializeField] private Entry[] _entries = { };

        [Serializable]
        internal sealed class Entry
        {
            public TEnum Key;
            public TValue Value;

            public Entry(TEnum key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            _entries = new Entry[_dictionary.Count];
            int i = 0;
            foreach (KeyValuePair<TEnum, TValue> kvp in _dictionary)
            {
                _entries[i++] = new Entry(kvp.Key, kvp.Value);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _dictionary.Clear();
            foreach (var entry in _entries)
            {
                _dictionary[entry.Key] = entry.Value;
            }
            _version++;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Gets a new pair entry for the dictionary.<br/>
        /// This method is used by the editor to create a new entry for the dictionary.<br/>
        /// It is not intended for runtime use and should not be called outside of the editor.
        /// </summary>
        /// <returns>A new <see cref="Entry"/> instance with default values for the key and value.</returns>
        internal static Entry Editor_GetNewPairEntry()
        {
            // var defaultKey = default(TEnum);
            var allEnums = Enum.GetValues(typeof(TEnum));
            TEnum defaultKey = (TEnum)allEnums.GetValue(0)!;

            TValue defaultValue = CreateNewValue<TValue>()!;
            return new Entry(defaultKey, defaultValue);
        }

        internal static T? CreateNewValue<T>()
        {
            if (typeof(T).IsValueType || typeof(Object).IsAssignableFrom(typeof(T)))
            {
                // If T is a value type or a Unity Object, we can safely return default (null for reference types).
                // This is useful for structs or Unity types that don't require a constructor.
                return default(T?);
            }

            var ctor = typeof(T).GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                return (T)Activator.CreateInstance(typeof(T));
            }

            return default(T?); // null for reference types
        }
#endif

        #endregion
    }

#if UNITY_EDITOR
 #pragma warning disable CS0618 // Type or member is obsolete // Justification: Obsoletion warning exists just to warn developers not to use the base class directly
    [CustomPropertyDrawer(typeof(EnumDictionary), useForChildren: true)]
 #pragma warning restore CS0618 // Type or member is obsolete
    sealed class EnumDictionary_PropertyDrawer : PropertyDrawer
    {
        private SerializedProperty? _defaultValueProp, _entriesProp;
        private bool _overridesFoldout = true;
        private const float _removeButtonWidth = 20f;
        private const float _separatorWidth = 4f;
        private float _keysWidth = 0.5f;
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private bool _draggingSeparator;
        private float _dragStartX;
        private float _dragStartWidth;

        [MemberNotNullWhen(true, nameof(_tempEntry), nameof(_tempEntrySerObj), nameof(_tempEntryKeyProp), nameof(_tempEntryValueProp))]
        private bool AddingNewEntry { get; set; }

        private NewEntrySO? _tempEntry;
        private SerializedObject? _tempEntrySerObj;
        private SerializedProperty? _tempEntryKeyProp, _tempEntryValueProp;

        sealed class NewEntrySO : ScriptableObject
        {
            [SerializeReference] public object Entry = null!;

            public static NewEntrySO CreateInstanceFromDictionaryField(FieldInfo field)
            {
                // 1. get the method 'Editor_GetNewPairEntry' from the EnumDictionary type
                MethodInfo? method = field.FieldType.GetMethod("Editor_GetNewPairEntry", BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                    throw new InvalidOperationException($"Could not find Editor_GetNewPairEntry method in {field.FieldType} type.");

                // 2. invoke the method to get a new entry
                object? entry = method.Invoke(null, null);
                if (entry == null)
                    throw new InvalidOperationException("Editor_GetNewPairEntry returned null.");

                // 3. create a new ScriptableObject instance and set the Entry property, then return the result
                NewEntrySO newEntry = CreateInstance<NewEntrySO>();
                newEntry.hideFlags = HideFlags.HideAndDontSave;
                newEntry.Entry = entry;
                return newEntry;
            }
        }

        private bool IsInitialized => _defaultValueProp != null && _entriesProp != null;

        private void Initialize(SerializedProperty property)
        {
            if (IsInitialized) return;
            _defaultValueProp = property.FindPropertyRelative("_defaultValue");
            _entriesProp = property.FindPropertyRelative("_entries");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Initialize(property);

            // Base height: the foldout header
            float h = EditorGUIUtility.singleLineHeight
                + EditorGUIUtility.standardVerticalSpacing;

            // If the whole property is collapsed, stop here
            bool isFolded = _foldoutStates.GetValueOrDefault(property.propertyPath, true);
            if (!isFolded)
                return h;

            // 1) Default Value field
            h += EditorGUI.GetPropertyHeight(_defaultValueProp!, true)
                + EditorGUIUtility.standardVerticalSpacing;

            // 2) Overrides foldout header
            h += EditorGUIUtility.singleLineHeight
                + EditorGUIUtility.standardVerticalSpacing;

            if (_overridesFoldout)
            {
                // 3) Existing entries
                for (int i = 0; i < _entriesProp!.arraySize; i++)
                {
                    var entry = _entriesProp.GetArrayElementAtIndex(i);
                    var keyProp = entry.FindPropertyRelative("Key");
                    var valProp = entry.FindPropertyRelative("Value");
                    float neededHeight = Mathf.Max(
                        EditorGUI.GetPropertyHeight(keyProp, true),
                        EditorGUI.GetPropertyHeight(valProp, true)
                    );
                    neededHeight = Mathf.Max(neededHeight, EditorGUIUtility.singleLineHeight); // Ensure at least one line height
                    h += neededHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                // 4) "Add Entry" button line
                h += EditorGUIUtility.singleLineHeight
                    + EditorGUIUtility.standardVerticalSpacing;

                // 5) If we're in the middle of adding a new entry, reserve extra space:
                if (AddingNewEntry && _tempEntrySerObj != null)
                {
                    //   Key field
                    h += EditorGUIUtility.singleLineHeight
                        + EditorGUIUtility.standardVerticalSpacing;

                    //   Value field (could be multi-line)
                    h += EditorGUI.GetPropertyHeight(_tempEntryValueProp!, true)
                        + EditorGUIUtility.standardVerticalSpacing;

                    //   Confirm/Cancel buttons
                    h += EditorGUIUtility.singleLineHeight
                        + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Initialize(property);
            // foldout
            bool fold = _foldoutStates.GetValueOrDefault(property.propertyPath, true);
            var foldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            fold = EditorGUI.Foldout(foldRect, fold, label, true);
            _foldoutStates[property.propertyPath] = fold;
            if (!fold) return;

            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // default value
            var dvRect = new Rect(
                position.x, y, position.width,
                EditorGUI.GetPropertyHeight(_defaultValueProp!, true)
            );
            EditorGUI.PropertyField(dvRect, _defaultValueProp!, new GUIContent("Default Value"), true);
            y += dvRect.height + EditorGUIUtility.standardVerticalSpacing;

            // draw overrides
            DrawEntries(new Rect(position.x, y, position.width, position.height - (y - position.y)), property);

            EditorGUI.indentLevel--;
        }

        private void DrawEntries(Rect rect, SerializedProperty property)
        {
            var entries = _entriesProp!;
            float line = EditorGUIUtility.singleLineHeight;
            float sp = EditorGUIUtility.standardVerticalSpacing;
            float x = rect.x;
            float y = rect.y;

            // Overrides header
            var hdrRect = new Rect(x, y, rect.width, line);
            _overridesFoldout = EditorGUI.Foldout(hdrRect, _overridesFoldout, "Overrides", true, EditorStyles.foldoutHeader);
            y += line + sp;
            if (!_overridesFoldout) return;

            var indentRect = EditorGUI.IndentedRect(new Rect(x, y, rect.width, rect.height - (y - rect.y)));
            float totalW = indentRect.width;
            float keyW = Mathf.Max(60, _keysWidth * (totalW - _removeButtonWidth - _separatorWidth));
            float valueW = totalW - keyW - _removeButtonWidth - _separatorWidth;

            // existing entries...
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var keyProp = entry.FindPropertyRelative("Key");
                var valProp = entry.FindPropertyRelative("Value");
                float neededHeight = Mathf.Max(
                    EditorGUI.GetPropertyHeight(keyProp, true),
                    EditorGUI.GetPropertyHeight(valProp, true)
                );
                neededHeight = Mathf.Max(neededHeight, line); // Ensure at least one line height
                
                var rowKey = new Rect(indentRect.x, y, keyW, neededHeight);
                var sep = new Rect(rowKey.xMax, y, _separatorWidth, neededHeight);
                var rowVal = new Rect(sep.xMax, y, valueW, neededHeight);
                var rem = new Rect(rowVal.xMax, y, _removeButtonWidth, neededHeight);

                // EditorGUI.DrawRect(rowKey, new Color(1f, 0f, 0f, 0.3f));
                // EditorGUI.DrawRect(sep, new Color(0f, 1f, 0f, 0.3f));
                // EditorGUI.DrawRect(rowVal, new Color(0f, 0f, 1f, 0.3f));
                // EditorGUI.DrawRect(rem, new Color(1f, 1f, 0f, 0.3f));

                EditorGUI.PropertyField(rowKey, keyProp, GUIContent.none);
                EditorGUIUtility.AddCursorRect(sep, MouseCursor.ResizeHorizontal);
                HandleSeparatorDrag(sep, totalW);
                // Draw value property, showing children for custom classes
                if (valProp.propertyType == SerializedPropertyType.Generic && valProp.hasVisibleChildren)
                {
                    float prevLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Min(80f, prevLabelWidth);
                    EditorGUI.PropertyField(rowVal, valProp, new GUIContent(valProp.displayName), true);
                    EditorGUIUtility.labelWidth = prevLabelWidth;
                }
                else
                {
                    EditorGUI.PropertyField(rowVal, valProp, GUIContent.none);
                }
                if (GUI.Button(rem, EditorGUIUtility.IconContent("Toolbar Minus", "Remove Entry"), EditorStyles.iconButton))
                {
                    entries.DeleteArrayElementAtIndex(i);
                    property.serializedObject.ApplyModifiedProperties();
                    return;
                }

                y += neededHeight + sp;
            }

            // "Add Entry" button
            var addRect = new Rect(x, y, rect.width, line);
            if (!AddingNewEntry)
            {
                const float ellipsisBtnWidth = 18f;
                Rect addBtnRect = new Rect(addRect.x, addRect.y, addRect.width - ellipsisBtnWidth, line);
                Rect ellipsisRect = new Rect(addBtnRect.xMax, addBtnRect.y, ellipsisBtnWidth, line);

                if (GUI.Button(addBtnRect, EditorGUIUtility.TrTextContentWithIcon("Add Entry", "Toolbar Plus"), EditorStyles.toolbarButton))
                {
                    // 1) Create our buffered SO via the helper
                    _tempEntry = NewEntrySO.CreateInstanceFromDictionaryField(fieldInfo);
                    _tempEntrySerObj = new SerializedObject(_tempEntry);

                    // 2) Grab the wrapper ( Entry ) and its Key/Value props
                    var wrapper = _tempEntrySerObj.FindProperty(nameof(NewEntrySO.Entry))!;
                    _tempEntryKeyProp = wrapper.FindPropertyRelative("Key");
                    _tempEntryValueProp = wrapper.FindPropertyRelative("Value");

                    AddingNewEntry = true;
                }

                if (GUI.Button(ellipsisRect, GUIContent.none, EditorStyles.toolbarDropDown))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(
                        new GUIContent("Add All Missing Overrides"), false, () =>
                        {
                            // Collect all enum values
                            string[]? enumType = _entriesProp!.GetArrayElementAtIndex(0).FindPropertyRelative("Key").propertyType == SerializedPropertyType.Enum
                                ? _entriesProp.GetArrayElementAtIndex(0).FindPropertyRelative("Key").enumNames
                                : null;
                            if (enumType == null) return;

                            // Find missing enum indices
                            HashSet<int> usedIndices = new();
                            for (int i = 0; i < _entriesProp.arraySize; i++)
                            {
                                var e = _entriesProp.GetArrayElementAtIndex(i);
                                usedIndices.Add(e.FindPropertyRelative("Key").enumValueIndex);
                            }

                            // Add missing
                            for (int i = 0; i < enumType.Length; i++)
                            {
                                if (!usedIndices.Contains(i))
                                {
                                    int idx = _entriesProp.arraySize;
                                    _entriesProp.InsertArrayElementAtIndex(idx);
                                    var newEnt = _entriesProp.GetArrayElementAtIndex(idx);
                                    newEnt.FindPropertyRelative("Key").enumValueIndex = i;
                                    // Value: use default
                                    // (Assumes default value is set in the dictionary)
                                }
                            }
                            property.serializedObject.ApplyModifiedProperties();
                        }
                    );
                    // menu.AddItem(
                    //     new GUIContent("Sort Overrides"), false, () =>
                    //     {
                    //         // Sort entries by key
                    //         entries.arraySize = entries.arraySize; // Force re-evaluation of array size
                    //         entries.serializedObject.Update();
                    //         entries.Sort((a, b) =>
                    //             {
                    //                 var keyA = a.FindPropertyRelative("Key").enumValueIndex;
                    //                 var keyB = b.FindPropertyRelative("Key").enumValueIndex;
                    //                 return keyA.CompareTo(keyB);
                    //             }
                    //         );
                    //         property.serializedObject.ApplyModifiedProperties();
                    //     }
                    // );
                    menu.AddItem(
                        new GUIContent("Remove Overrides Matching Default Value"), false, () =>
                        {
                            // Get value comparer from the dictionary
                            object dict = property.boxedValue;
                            FieldInfo valueComparerField = dict.GetType().GetField("_valueComparer", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                throw new InvalidOperationException("Could not find _valueComparer field in the dictionary type.");
                            // Type is actually IEqualityComparer<TValue>, but the strongly-typed version derives from the non-generic IEqualityComparer
                            IEqualityComparer valueComparer = (IEqualityComparer)valueComparerField.GetValue(dict) ??
                                throw new InvalidOperationException("Could not get _valueComparer field value from the dictionary instance.");

                            // Remove entries that match the default value
                            int numRemoved = 0;
                            for (int i = _entriesProp!.arraySize - 1; i >= 0; i--)
                            {
                                var e = _entriesProp.GetArrayElementAtIndex(i);
                                if (valueComparer.Equals(e.FindPropertyRelative("Value").boxedValue, _defaultValueProp!.boxedValue))
                                {
                                    numRemoved++;
                                    _entriesProp.DeleteArrayElementAtIndex(i);
                                }
                            }
                            if (numRemoved > 0)
                            {
                                property.serializedObject.ApplyModifiedProperties();
                                Debug.Log($"Removed {numRemoved} overrides matching the default value.");
                            }
                            else
                            {
                                Debug.Log("No overrides matched the default value.");
                            }
                        }
                    );
                    menu.DropDown(ellipsisRect);
                }
            }
            else
            {
                // Calculate box height
                float valH = EditorGUI.GetPropertyHeight(_tempEntryValueProp!, true);
                float boxHeight = (line + sp) // Key field
                    + (valH + sp) // Value field
                    + (line + sp); // Buttons

                var boxRect = new Rect(x, y, rect.width, boxHeight);
                GUI.Box(boxRect, GUIContent.none);

                float innerY = y + 2; // Small padding
                float innerX = x + 2;
                float innerW = rect.width - 4;

                // Ensure our serialized object is up to date
                _tempEntrySerObj!.Update();

                // Draw Key field
                var keyRect = new Rect(innerX, innerY, innerW, line);
                DrawNewKeyPropertyField(keyRect, _tempEntryKeyProp!, entries);
                innerY += line + sp;

                // Draw Value field
                var valRect = new Rect(innerX, innerY, innerW, valH);
                DrawNewValuePropertyField(valRect, _tempEntryValueProp!);
                innerY += valH + sp;

                _tempEntrySerObj.ApplyModifiedProperties();

                // Confirm / Cancel buttons
                var btnRow = new Rect(innerX, innerY, innerW, line);
                var can = new Rect(btnRow.x, btnRow.y, btnRow.width * 0.5f, line);
                var ok = new Rect(can.xMax, btnRow.y, btnRow.width * 0.5f, line);

                if (GUI.Button(can, EditorGUIUtility.TrTextContentWithIcon("Cancel", "Toolbar Minus"), EditorStyles.miniButtonLeft))
                {
                    Object.DestroyImmediate(_tempEntry);
                    _tempEntry = null;
                    AddingNewEntry = false;
                }

                // disable Confirm if duplicate key
                bool dup = false;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var e = entries.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("Key").enumValueIndex == _tempEntryKeyProp!.enumValueIndex)
                    {
                        dup = true;
                        break;
                    }
                }

                EditorGUI.BeginDisabledGroup(dup);
                if (GUI.Button(ok, EditorGUIUtility.TrTextContentWithIcon("Confirm", "Toolbar Plus"), EditorStyles.miniButtonRight))
                {
                    // Append to real array
                    int idx = entries.arraySize;
                    entries.InsertArrayElementAtIndex(idx);
                    var newEnt = entries.GetArrayElementAtIndex(idx);
                    newEnt.FindPropertyRelative("Key").enumValueIndex = _tempEntryKeyProp.enumValueIndex;
                    newEnt.FindPropertyRelative("Value").boxedValue = _tempEntryValueProp!.boxedValue;
                    property.serializedObject.ApplyModifiedProperties();

                    // cleanup
                    Object.DestroyImmediate(_tempEntry);
                    _tempEntry = null;
                    AddingNewEntry = false;
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private static void DrawNewKeyPropertyField(Rect rect, SerializedProperty keyProp, SerializedProperty entries)
        {
            // Collect used indices
            HashSet<int> usedIndices = new();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                usedIndices.Add(e.FindPropertyRelative("Key").enumValueIndex);
            }

            int currentIndex = keyProp.enumValueIndex;
            string currentName = currentIndex >= 0 && currentIndex < keyProp.enumDisplayNames.Length ? keyProp.enumDisplayNames[currentIndex] : "Select Key";

            rect = EditorGUI.PrefixLabel(rect, new GUIContent("New Key"));
            if (EditorGUI.DropdownButton(rect, new GUIContent(currentName), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                for (int i = 0; i < keyProp.enumDisplayNames.Length; i++)
                {
                    bool used = usedIndices.Contains(i);
                    int idx = i;
                    menu.AddItem(
                        new GUIContent(keyProp.enumDisplayNames[i]), currentIndex == i, () =>
                        {
                            if (!used)
                            {
                                keyProp.enumValueIndex = idx;
                                keyProp.serializedObject.ApplyModifiedProperties();
                            }
                        }
                    );
                    if (used)
                        menu.AddDisabledItem(new GUIContent(keyProp.enumDisplayNames[i]));
                }
                menu.DropDown(rect);
            }
        }

        private static void DrawNewValuePropertyField(Rect valRect, SerializedProperty valueProp)
        {
            EditorGUI.PropertyField(valRect, valueProp, new GUIContent("New Value"), true);
            // TODO: Above field is drawn disabled for some reason; create drawing workaround (e.g. manually iterate children and draw them)
        }

        private void HandleSeparatorDrag(Rect sep, float totalWidth)
        {
            if (Event.current.type == EventType.MouseDown && sep.Contains(Event.current.mousePosition))
            {
                _draggingSeparator = true;
                _dragStartX = Event.current.mousePosition.x;
                _dragStartWidth = _keysWidth;
                Event.current.Use();
            }
            if (_draggingSeparator && Event.current.type == EventType.MouseDrag)
            {
                float delta = Event.current.mousePosition.x - _dragStartX;
                _keysWidth = Mathf.Clamp01(_dragStartWidth + delta / (totalWidth - _removeButtonWidth - _separatorWidth));
                GUI.changed = true;
                Event.current.Use();
            }
            if (_draggingSeparator && Event.current.type == EventType.MouseUp)
            {
                _draggingSeparator = false;
                Event.current.Use();
            }
        }
    }
#endif
}
