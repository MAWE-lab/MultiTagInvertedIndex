using Collections.Special;

namespace MultiTagInvertedIndex.Rules
{
    /// <summary>
    /// Base rule class.
    /// </summary>
    /// <typeparam name="TTag">Tag type.</typeparam>
    public abstract class Rule<TTag>
    {
        /// <summary>
        /// Computes resulting bitmap for the rule.
        /// </summary>
        /// <param name="bitmapByTag">Function that returns bitmap associated with a tag.</param>
        /// <param name="allValues">Bitmap that contains all available values.</param>
        /// <returns>Result bitmap.</returns>
        public abstract RoaringBitmap Compute(Func<TTag, RoaringBitmap> bitmapByTag, RoaringBitmap allValues);

        /// <summary>
        /// And operator for rules.
        /// </summary>
        /// <param name="left">First rule.</param>
        /// <param name="right">Second rule.</param>
        /// <returns>Result <see cref="AndRule{TTag}"/> object.</returns>
        public static AndRule<TTag> operator &(Rule<TTag> left, Rule<TTag> right) => new(left, right);
        /// <summary>
        /// Or operator for rules.
        /// </summary>
        /// <param name="left">First rule.</param>
        /// <param name="right">Second rule.</param>
        /// <returns>Result <see cref="OrRule{TTag}"/> object.</returns>
        public static OrRule<TTag> operator |(Rule<TTag> left, Rule<TTag> right) => new(left, right);
        /// <summary>
        /// Xor operator for rules.
        /// </summary>
        /// <param name="left">First rule.</param>
        /// <param name="right">Second rule.</param>
        /// <returns>Result <see cref="XorRule{TTag}"/> object.</returns>
        public static XorRule<TTag> operator ^(Rule<TTag> left, Rule<TTag> right) => new(left, right);
        /// <summary>
        /// Not operator for rules.
        /// </summary>
        /// <param name="rule">Rule to negate.</param>
        /// <returns>Result <see cref="NotRule{TTag}"/> object.</returns>
        public static NotRule<TTag> operator ~(Rule<TTag> rule) => new(rule);
        /// <summary>
        /// Creates a rule matching the specified tag.
        /// </summary>
        /// <param name="tag">Tag to match.</param>
        public static implicit operator Rule<TTag>(TTag tag) => new SingleRule<TTag>(tag);
    }

    /// <summary>
    /// Rule representing single tag.
    /// </summary>
    /// <typeparam name="TTag">Tag type.</typeparam>
    /// <param name="tag">Associated tag.</param>
    public sealed class SingleRule<TTag>(TTag tag) : Rule<TTag>
    {
        private readonly TTag _tag = tag ?? throw new ArgumentNullException(nameof(tag));

        /// <inheritdoc/>
        public override RoaringBitmap Compute(Func<TTag, RoaringBitmap> bitmapByTag, RoaringBitmap allValues) =>
            bitmapByTag(_tag);
    }

    /// <summary>
    /// Base class for rule containing child rules.
    /// </summary>
    /// <typeparam name="TTag">Tag type.</typeparam>
    public abstract class CompositeRule<TTag> : Rule<TTag>
    {
        /// <summary>
        /// Child rules array.
        /// </summary>
        protected readonly Rule<TTag>[] _childRules;
        /// <summary>
        /// Returns identity bitmap for the operation.
        /// </summary>
        protected abstract RoaringBitmap Identity(RoaringBitmap allValues);
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="rules">Child rules.</param>
        /// <exception cref="ArgumentException">Thrown if given collection is empty.</exception>
        protected CompositeRule(params Rule<TTag>[] rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            if (rules.Length == 0)
                throw new ArgumentException("Rules collection must not be empty", nameof(rules));
            _childRules = rules;
        }

        /// <summary>
        /// Executes binary bitmap operation.
        /// </summary>
        protected abstract RoaringBitmap Execute(RoaringBitmap left, RoaringBitmap right);
        /// <inheritdoc/>
        public override RoaringBitmap Compute(Func<TTag, RoaringBitmap> bitmapByTag, RoaringBitmap allValues)
        {
            RoaringBitmap result = Identity(allValues);
            foreach (Rule<TTag> rule in _childRules)
                result = Execute(result, rule.Compute(bitmapByTag, allValues));
            return result;
        }
    }

    /// <summary>
    /// Rule matching values that satisfy all child rules.
    /// </summary>
    /// <typeparam name="TTag">Tag type.</typeparam>
    /// <param name="rules">Child rules.</param>
    public sealed class AndRule<TTag>(params Rule<TTag>[] rules) : CompositeRule<TTag>(rules)
    {
        /// <inheritdoc/>
        protected override RoaringBitmap Execute(RoaringBitmap left, RoaringBitmap right) => left & right;

        /// <inheritdoc/>
        protected override RoaringBitmap Identity(RoaringBitmap allValues) => RoaringBitmap.Create(allValues);

        /// <summary>
        /// And operator for rules.
        /// </summary>
        /// <param name="left">First rule.</param>
        /// <param name="right">Second rule.</param>
        /// <returns>Result <see cref="AndRule{TTag}"/> object.</returns>
        public static AndRule<TTag> operator &(AndRule<TTag> left, AndRule<TTag> right) =>
            new([.. left._childRules, .. right._childRules]);
    }

    /// <summary>
    /// Rule matching values that satisfy at least one child rule.
    /// </summary>
    /// <typeparam name="TTag">Tag type.</typeparam>
    /// <param name="rules">Child rules.</param>
    public sealed class OrRule<TTag>(params Rule<TTag>[] rules) : CompositeRule<TTag>(rules)
    {
        /// <inheritdoc/>
        protected override RoaringBitmap Execute(RoaringBitmap left, RoaringBitmap right) => left | right;

        /// <inheritdoc/>
        protected override RoaringBitmap Identity(RoaringBitmap allValues) => RoaringBitmap.Create();

        /// <summary>
        /// Or operator for rules.
        /// </summary>
        /// <param name="left">First rule.</param>
        /// <param name="right">Second rule.</param>
        /// <returns>Result <see cref="OrRule{TTag}"/> object.</returns>
        public static OrRule<TTag> operator |(OrRule<TTag> left, OrRule<TTag> right) =>
            new([.. left._childRules, .. right._childRules]);
    }

    /// <summary>
    /// Rule matching values that satisfy an odd number of child rules.
    /// </summary>
    /// <typeparam name="TTag">Tag type.</typeparam>
    /// <param name="rules">Child rules.</param>
    public sealed class XorRule<TTag>(params Rule<TTag>[] rules) : CompositeRule<TTag>(rules)
    {
        /// <inheritdoc/>
        protected override RoaringBitmap Execute(RoaringBitmap left, RoaringBitmap right) => left ^ right;

        /// <inheritdoc/>
        protected override RoaringBitmap Identity(RoaringBitmap allValues) => RoaringBitmap.Create();

        /// <summary>
        /// Xor operator for rules.
        /// </summary>
        /// <param name="left">First rule.</param>
        /// <param name="right">Second rule.</param>
        /// <returns>Result <see cref="XorRule{TTag}"/> object.</returns>
        public static XorRule<TTag> operator ^(XorRule<TTag> left, XorRule<TTag> right) =>
            new([.. left._childRules, .. right._childRules]);
    }

    /// <summary>
    /// Rule matching values that do not satisfy the inner rule.
    /// </summary>
    /// <typeparam name="TTag">Tag type.</typeparam>
    /// <param name="rule">Child rule.</param>
    public sealed class NotRule<TTag>(Rule<TTag> rule) : Rule<TTag>
    {
        private readonly Rule<TTag> _rule = rule ?? throw new ArgumentNullException(nameof(rule));

        /// <inheritdoc/>
        public override RoaringBitmap Compute(Func<TTag, RoaringBitmap> bitmapByTag, RoaringBitmap allValues) =>
            RoaringBitmap.AndNot(allValues, _rule.Compute(bitmapByTag, allValues));
    }

    /// <summary>
    /// Provides extension methods and factory helpers for <see cref="Rule{TTag}"/>.
    /// </summary>
    public static class Rule
    {
        /// <summary>
        /// Creates a rule matching the specified tag.
        /// </summary>
        /// <typeparam name="TTag">Tag type.</typeparam>
        /// <param name="tag">Tag to match.</param>
        /// <returns>A <see cref="Rule{TTag}"/> matching the specified tag.</returns>
        public static Rule<TTag> Tag<TTag>(TTag tag) => new SingleRule<TTag>(tag);
    }
}
