using MultiTagInvertedIndex.Rules;
using Collections.Special;

#pragma warning disable IDE0130
namespace Indexing
#pragma warning restore IDE0130
{
    /// <summary>
    /// Represents a collection that associates <typeparamref name="TValue"/> objects
    /// with unordered collections of <typeparamref name="TTag"/> objects
    /// and allows querying values using <see cref="Rule{TTag}"/>.
    /// </summary>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <typeparam name="TTag">Tag type.</typeparam>
    public class MultiTagIndex<TValue, TTag>
    {
        private readonly Dictionary<KeyWrapper<TTag>, RoaringBitmap> _tagValueMap = [];
        private readonly Dictionary<KeyWrapper<TValue>, HashSet<KeyWrapper<TTag>>> _tagsByValue = [];
        
        private readonly Dictionary<int, TValue> _valueById = [];
        private readonly Dictionary<KeyWrapper<TValue>, int> _idByValue = [];
        private int _nextValueId = 0;

        private RoaringBitmap _allValues = RoaringBitmap.Create();

        /// <summary>
        /// Adds a tag to the collection.
        /// </summary>
        /// <param name="tag">Tag to add.</param>
        /// <exception cref="ArgumentException">Thrown when tag already exists.</exception>
        public void AddTag(TTag tag)
        {
            if (!TryAddTag(tag))
                throw new ArgumentException($"Tag {nameof(tag)} already exists");
        }

        /// <summary>
        /// Adds a tag to the collection.
        /// </summary>
        /// <param name="tag">Tag to add.</param>
        /// <returns><see langword="true"/> if tag was added;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryAddTag(TTag tag)
        {
            if (_tagValueMap.ContainsKey(tag))
                return false;
            _tagValueMap[tag] = RoaringBitmap.Create();
            return true;
        }

        /// <summary>
        /// Removes the tag from the collection.
        /// </summary>
        /// <param name="tag">Tag to remove.</param>
        /// <exception cref="KeyNotFoundException">Thrown when tag hasn't been found.</exception>
        public void RemoveTag(TTag tag)
        {
            if (!TryRemoveTag(tag))
                throw new KeyNotFoundException($"Tag {nameof(tag)} is not found");
        }

        /// <summary>
        /// Removes the tag from the collection.
        /// </summary>
        /// <param name="tag">Tag to remove.</param>
        /// <returns><see langword="true"/> if tag was removed;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryRemoveTag(TTag tag)
        {
            if (!_tagValueMap.TryGetValue(tag, out RoaringBitmap? bitmap))
                return false;
            foreach (int id in bitmap)
            {
                TValue value = _valueById[id];
                _tagsByValue[value].Remove(tag);
            }
            _tagValueMap.Remove(tag);
            return true;
        }

        /// <summary>
        /// Adds a value to the collection.
        /// </summary>
        /// <param name="v">Value to add.</param>
        /// <exception cref="ArgumentException">Thrown when value already exists.</exception>
        public void AddValue(TValue v)
        {
            if (!TryAddValue(v))
                throw new ArgumentException($"Value {nameof(v)} already exists");
        }

        /// <summary>
        /// Adds a value to the collection.
        /// </summary>
        /// <param name="v">Value to add.</param>
        /// <returns><see langword="true"/> if value was added;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryAddValue(TValue v)
        {
            if (_idByValue.ContainsKey(v))
                return false;
            _valueById[_nextValueId] = v;
            _idByValue[v] = _nextValueId;
            _tagsByValue[v] = [];
            _allValues |= RoaringBitmap.Create(_nextValueId);
            _nextValueId++;
            return true;
        }

        /// <summary>
        /// Removes the specified value from the collection.
        /// </summary>
        /// <param name="v">Value to remove.</param>
        /// <exception cref="ArgumentException">Thrown when value hasn't been found.</exception>
        public void RemoveValue(TValue v)
        {
            if (!TryRemoveValue(v))
                throw new ArgumentException($"Value {nameof(v)} is not found");
        }

        /// <summary>
        /// Removes the specified value from the collection.
        /// </summary>
        /// <param name="v">Value to remove.</param>
        /// <returns><see langword="true"/> if value was removed;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryRemoveValue(TValue v)
        {
            if (!_tagsByValue.TryGetValue(v, out HashSet<KeyWrapper<TTag>>? tags))
                return false;
            _tagsByValue.Remove(v);
            int id = _idByValue[v];
            _valueById.Remove(id);
            _idByValue.Remove(v);
            _allValues = RoaringBitmap.AndNot(_allValues, RoaringBitmap.Create(id));
            foreach (KeyWrapper<TTag> tag in tags)
                _tagValueMap[tag] = RoaringBitmap.AndNot(_tagValueMap[tag], RoaringBitmap.Create(id));
            return true;
        }

        /// <summary>
        /// Gets all values matching the specified rule.
        /// </summary>
        /// <param name="rule">Rule used to compute the result set.</param>
        /// <returns>A collection of <typeparamref name="TValue"/> objects.</returns>
        public IEnumerable<TValue> this[Rule<TTag> rule]
        {
            get => EnumerateValues(rule);
        }

        /// <summary>
        /// Gets or sets the collection of <typeparamref name="TTag"/> objects
        /// associated with the specified value.
        /// </summary>
        /// <param name="v">Value.</param>
        /// <returns>A collection of <typeparamref name="TTag"/> objects.</returns>
        public IEnumerable<TTag> this[TValue v]
        {
            get => [.. _tagsByValue[v].Select(t => t.Value)];
            set
            {
                TryRemoveValue(v);
                AddTags(v, [.. value]);
            }
        }

        /// <summary>
        /// Associates tags with the specified value.
        /// </summary>
        /// <param name="v">Target value.</param>
        /// <param name="tags">The collection of tags to add.</param>
        public void AddTags(TValue v, IEnumerable<TTag> tags)
        {
            TryAddValue(v);
            int valueId = _idByValue[v];
            foreach (TTag tag in tags)
            {
                TryAddTag(tag);
                _tagValueMap[tag] |= RoaringBitmap.Create(valueId);
                _tagsByValue[v].Add(tag);
            }
        }

        /// <summary>
        /// Associates tags with the specified value.
        /// </summary>
        /// <param name="v">Target value.</param>
        /// <param name="tags">The collection of tags to add.</param>
        public void AddTags(TValue v, params TTag[] tags) => AddTags(v, (IEnumerable<TTag>)tags);

        /// <summary>
        /// Removes associations between the specified value and tags.
        /// </summary>
        /// <param name="v">Target value.</param>
        /// <param name="tags">The collection of tags to remove.</param>
        /// <exception cref="KeyNotFoundException">Thrown when value or one of the tags hasn't been found.</exception>
        public void RemoveTags(TValue v, IEnumerable<TTag> tags)
        {
            if (!_idByValue.ContainsKey(v))
                throw new KeyNotFoundException($"Value {nameof(v)} is not found");
            foreach (TTag tag in tags)
                if (!_tagValueMap.ContainsKey(tag))
                    throw new KeyNotFoundException($"Tag {nameof(tag)} is not found");
            TryRemoveTags(v, tags);
        }

        /// <summary>
        /// Removes associations between the specified value and tags.
        /// </summary>
        /// <param name="v">Target value.</param>
        /// <param name="tags">The collection of tags to remove.</param>
        /// <exception cref="KeyNotFoundException">Thrown when value or one of the tags hasn't been found.</exception>
        public void RemoveTags(TValue v, params TTag[] tags) => RemoveTags(v, (IEnumerable<TTag>)tags);


        /// <summary>
        /// Removes associations between the specified value and tags.
        /// </summary>
        /// <param name="v">Target value.</param>
        /// <param name="tags">The collection of tags to remove.</param>
        /// <returns><see langword="true"/> if tags were removed;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryRemoveTags(TValue v, IEnumerable<TTag> tags)
        {
            if (!_idByValue.ContainsKey(v))
                return false;
            int valueId = _idByValue[v];
            foreach (TTag tag in tags)
                if (!_tagValueMap.ContainsKey(tag))
                    return false;
            foreach (TTag tag in tags)
            {
                _tagValueMap[tag] = RoaringBitmap.AndNot(_tagValueMap[tag], RoaringBitmap.Create(valueId));
                _tagsByValue[v].Remove(tag);
            }
            return true;
        }

        /// <summary>
        /// Removes associations between the specified value and tags.
        /// </summary>
        /// <param name="v">Target value.</param>
        /// <param name="tags">The collection of tags to remove.</param>
        /// <returns><see langword="true"/> if tags were removed;
        /// otherwise, <see langword="false"/>.</returns>
        public bool TryRemoveTags(TValue v, params TTag[] tags) => TryRemoveTags(v, (IEnumerable<TTag>)tags);

        /// <summary>
        /// Determines whether any value matches the specified rule.
        /// </summary>
        /// <param name="rule">Rule used to compute a set of values.</param>
        /// <returns><see langword="true"/> if at least one value matches the specified rule;
        /// otherwise, <see langword="false"/>.</returns>
        public bool Contains(Rule<TTag> rule) => rule.Compute(GetBitmapByTag, _allValues).Cardinality != 0;

        /// <summary>
        /// Counts values matching the specified rule.
        /// </summary>
        /// <param name="rule">Rule used to compute the set.</param>
        /// <returns>The number of values matching the specified rule.</returns>
        public int Count(Rule<TTag> rule) => checked((int)rule.Compute(GetBitmapByTag, _allValues).Cardinality);

        /// <summary>
        /// Gets all values matching the specified rule.
        /// </summary>
        /// <param name="rule">Rule used to compute the result set.</param>
        /// <returns>A collection of <typeparamref name="TValue"/> objects.</returns>
        public List<TValue> GetValues(Rule<TTag> rule)
        {
            List<TValue> result = [];
            RoaringBitmap mask = rule.Compute(GetBitmapByTag, _allValues);
            result.Capacity = (int)mask.Cardinality;
            foreach (int index in mask)
                result.Add(_valueById[index]);
            return result;
        }

        private RoaringBitmap GetBitmapByTag(TTag tag)
        {
            if (_tagValueMap.TryGetValue(tag, out RoaringBitmap? result))
                return result;
            return RoaringBitmap.Create();
        }

        /// <summary>
        /// Lazily enumerates values matching the specified rule.
        /// </summary>
        /// <param name="rule">Rule used to compute the result set.</param>
        /// <returns>An enumerable sequence of matching <typeparamref name="TValue"/> objects.</returns>
        public IEnumerable<TValue> EnumerateValues(Rule<TTag> rule)
        {
            RoaringBitmap mask = rule.Compute(GetBitmapByTag, _allValues);

            foreach (int index in mask)
                yield return _valueById[index];
        }

        private readonly struct KeyWrapper<TKey>(TKey value) : IEquatable<KeyWrapper<TKey>>
        {
            public TKey Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

            public bool Equals(KeyWrapper<TKey> other) =>
                EqualityComparer<TKey>.Default.Equals(Value, other.Value);
            public override bool Equals(object? obj) =>
                obj is KeyWrapper<TKey> wrapper && Equals(wrapper);
            public override int GetHashCode() =>
                EqualityComparer<TKey>.Default.GetHashCode(Value!);

            public static implicit operator KeyWrapper<TKey>(TKey value) => new(value);
            public static implicit operator TKey(KeyWrapper<TKey> wrapper) => wrapper.Value;
        }
    }
}
