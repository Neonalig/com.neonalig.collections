using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Neonalig.Collections
{
    /// <summary>
    /// A list that maintains elements with associated priorities. The element with the highest priority is considered the "effective" value.
    /// If multiple elements share the same priority, the one added first is considered the effective value.
    /// If the list is empty, a default value is used as the effective value.
    /// </summary>
    /// <typeparam name="TPriority">The type of the priority. Must be non-nullable.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    [Serializable]
    public class PriorityList<TPriority, TValue>
        where TPriority : notnull
    {
        private readonly IEqualityComparer<TPriority> _priorityEqualityComparer;
        private readonly IComparer<TPriority> _priorityComparer;
        private readonly PriorityPairComparer _priorityPairComparer;
        private readonly IEqualityComparer<TValue> _valueEqualityComparer;
        [FormerlySerializedAs("defaultValue")]
        [SerializeField] private TValue _defaultValue = default!;
        [FormerlySerializedAs("list")]
        [SerializeField] private List<PriorityPair> _list = new();

        /// <summary>
        /// Creates a new PriorityList with optional custom comparers for priority and value types.
        /// </summary>
        /// <param name="defaultValue">The default value to use when the list is empty.</param>
        /// <param name="priorityComparer">Comparer for comparing priorities. If null, the default comparer for TPriority will be used.</param>
        /// <param name="priorityEqualityComparer">Equality comparer for comparing priorities. If null, the default equality comparer for TPriority will be used.</param>
        /// <param name="valueComparer">Equality comparer for comparing values. If null, the default equality comparer for TValue will be used.</param>
        public PriorityList(
            TValue defaultValue = default!,
            IComparer<TPriority>? priorityComparer = null,
            IEqualityComparer<TPriority>? priorityEqualityComparer = null,
            IEqualityComparer<TValue>? valueComparer = null)
        {
            this._priorityComparer = priorityComparer ?? Comparer<TPriority>.Default;
            this._priorityEqualityComparer = priorityEqualityComparer ?? EqualityComparer<TPriority>.Default;
            _valueEqualityComparer = valueComparer ?? EqualityComparer<TValue>.Default;
            _priorityPairComparer = new PriorityPairComparer(this._priorityComparer);
        }

        [Serializable]
        private struct PriorityPair
        {
            public TPriority Priority;
            public TValue Value;

            public PriorityPair(TPriority priority, TValue value)
            {
                Priority = priority;
                Value = value;
            }
        }

        private class PriorityPairComparer : IComparer<PriorityPair>
        {
            private readonly IComparer<TPriority> _priorityComparer;

            public PriorityPairComparer(IComparer<TPriority> priorityComparer)
            {
                this._priorityComparer = priorityComparer;
            }

            public int Compare(PriorityPair x, PriorityPair y)
            {
                return _priorityComparer.Compare(x.Priority, y.Priority);
            }
        }

        /// <summary>
        /// Add a value with the given priority. If a value with the same priority already exists, it will be replaced with the new value.
        /// If the new value has the highest priority, the <see cref="EffectiveValueChanged"/> event will be triggered.
        /// </summary>
        /// <param name="priority">The priority of the value.</param>
        /// <param name="value">The value to add.</param>
        public void Add(TPriority priority, TValue value)
        {
            var newPair = new PriorityPair(priority, value);
            int index = _list.FindIndex(p => _priorityEqualityComparer.Equals(p.Priority, priority));
            bool wasEffective = _list.Count > 0 && _priorityComparer.Compare(priority, _list[0].Priority) > 0;
            var oldEffectiveValue = EffectiveValue;

            if (index >= 0)
            {
                // Replace existing value with the same priority
                _list[index] = newPair;
            }
            else
            {
                // Insert new pair in sorted order
                _list.Add(newPair);
                _list.Sort(_priorityPairComparer);
                _list.Reverse(); // Highest priority first
            }

            if (wasEffective || index == 0)
            {
                var newEffectiveValue = EffectiveValue;
                if (!_valueEqualityComparer.Equals(oldEffectiveValue, newEffectiveValue))
                {
                    EffectiveValueChanged?.Invoke(oldEffectiveValue, newEffectiveValue);
                }
            }
        }

        /// <summary>
        /// Remove the first occurrence of the given value. If the removed value was the effective value, the <see cref="EffectiveValueChanged"/> event will be triggered.
        /// </summary>
        /// <param name="value">The value to remove.</param>
        /// <returns>True if the value was found and removed, false otherwise.</returns>
        public bool Remove(TValue value)
        {
            int index = _list.FindIndex(p => _valueEqualityComparer.Equals(p.Value, value));
            if (index >= 0)
            {
                _list.RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove the first occurrence of the given priority. If the removed value was the effective value, the <see cref="EffectiveValueChanged"/> event will be triggered.
        /// </summary>
        /// <param name="priority">The priority to remove.</param>
        /// <returns>True if the priority was found and removed, false otherwise.</returns>
        public bool Remove(TPriority priority)
        {
            int index = _list.FindIndex(p => _priorityEqualityComparer.Equals(p.Priority, priority));
            if (index >= 0)
            {
                bool wasEffective = index == 0;
                var oldEffectiveValue = EffectiveValue;
                _list.RemoveAt(index);
                if (wasEffective)
                {
                    var newEffectiveValue = EffectiveValue;
                    if (!_valueEqualityComparer.Equals(oldEffectiveValue, newEffectiveValue))
                    {
                        EffectiveValueChanged?.Invoke(oldEffectiveValue, newEffectiveValue);
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Delegate for handling value changes.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        public delegate void ValueChangedHandler(TValue oldValue, TValue newValue);

        /// <summary>
        /// Triggered when the effective value changes.
        /// </summary>
        public event ValueChangedHandler? EffectiveValueChanged;

        /// <summary>
        /// Triggered when the default value changes.
        /// </summary>
        public event ValueChangedHandler? DefaultValueChanged;

        /// <summary>
        /// Gets the effective value, which is the value with the highest priority. If the list is empty, returns the default value.
        /// </summary>
        public TValue EffectiveValue
        {
            get
            {
                if (_list.Count == 0)
                {
                    return _defaultValue;
                }
                return _list[0].Value;
            }
        }

        /// <summary>
        /// Clears the list. If the effective value changes as a result, the <see cref="EffectiveValueChanged"/> event will be triggered. The default value remains unchanged.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }

        /// <summary>
        /// Gets the number of elements in the list. This does not include the default value.
        /// </summary>
        public int Count => _list.Count;

        /// <summary>
        /// Gets or sets the default value. <see cref="DefaultValueChanged"/> will be triggered. If the effective value changes as a result of setting a new default value, the <see cref="EffectiveValueChanged"/> event will be triggered.
        /// </summary>
        public TValue DefaultValue
        {
            get => _defaultValue;
            set
            {
                if (!_valueEqualityComparer.Equals(_defaultValue, value))
                {
                    var oldEffectiveValue = EffectiveValue;
                    var oldDefaultValue = _defaultValue;
                    _defaultValue = value;
                    DefaultValueChanged?.Invoke(oldDefaultValue, _defaultValue);
                    var newEffectiveValue = EffectiveValue;
                    if (!_valueEqualityComparer.Equals(oldEffectiveValue, newEffectiveValue))
                    {
                        EffectiveValueChanged?.Invoke(oldEffectiveValue, newEffectiveValue);
                    }
                }
            }
        }
    }
}
