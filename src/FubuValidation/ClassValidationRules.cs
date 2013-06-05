﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FubuCore.Reflection;
using FubuLocalization;
using FubuValidation.Fields;

namespace FubuValidation
{
    public interface IFieldConditionalExpression
    {
        void If(IFieldRuleCondition condition);
        void If<T>() where T : IFieldRuleCondition, new();
    }

    public interface IRuleRegistrationExpression : IFieldConditionalExpression
    {
    }

    public interface IFieldValidationExpression : IFieldConditionalExpression
    {
        IFieldValidationExpression Rule(IFieldValidationRule rule);
        IFieldValidationExpression Rule<T>() where T : IFieldValidationRule, new();
    }

    public class ClassValidationRules<T> : IValidationRegistration, IValidationSource where T : class
    {
        private readonly IList<RuleRegistrationExpression> _rules = new List<RuleRegistrationExpression>();
        private readonly IList<IValidationRule> _classRules = new List<IValidationRule>();

        public RuleRegistrationExpression Require(params Expression<Func<T, object>>[] properties)
        {
            var accessors = properties.Select(x => x.ToAccessor());
            var expression = new RuleRegistrationExpression(a => new RequiredFieldRule(), accessors);

            _rules.Add(expression);

            return expression;
        }

        public FieldValidationExpression Property(Expression<Func<T, object>> property)
        {
            return new FieldValidationExpression(this, property.ToAccessor());
        }

        public void Register<TClassRule>() where TClassRule : IValidationRule, new()
        {
            _classRules.Add(new TClassRule());
        }

        public void Register<TClassRule>(TClassRule rule) where TClassRule : IValidationRule
        {
            _classRules.Add(rule);
        }

        void IValidationRegistration.Register(ValidationGraph graph)
        {
            _rules.Each(r => r.Register(graph.Fields));
            graph.RegisterSource(this);
        }

        IEnumerable<IValidationRule> IValidationSource.RulesFor(Type type)
        {
            return type == typeof(T)
                ? _classRules
                : Enumerable.Empty<IValidationRule>();
        }


        public class RuleRegistrationExpression : IRuleRegistrationExpression
        {
            private Func<Accessor, IFieldValidationRule> _ruleSource;
            private readonly IEnumerable<Accessor> _accessors;

            public RuleRegistrationExpression(Func<Accessor, IFieldValidationRule> ruleSource, Accessor accessor)
                : this(ruleSource, new[] { accessor })
            {
            }

            public RuleRegistrationExpression(Func<Accessor, IFieldValidationRule> ruleSource, IEnumerable<Accessor> accessors)
            {
                _ruleSource = ruleSource;
                _accessors = accessors;
            }

            public void If(IFieldRuleCondition condition)
            {
                var innerSource = _ruleSource;
                _ruleSource = a => new ConditionalFieldRule<T>(condition, innerSource(a));
            }

            public void If<TCondition>() where TCondition : IFieldRuleCondition, new()
            {
                If(new TCondition());
            }

            public void If(Func<T, bool> condition)
            {
                If(FieldRuleCondition.For(condition));
            }

            public void If(Func<T, ValidationContext, bool> condition)
            {
                If(FieldRuleCondition.For(condition));
            }

            internal void Register(IFieldRulesRegistry registration)
            {
                _accessors.Each(a => registration.Register(typeof(T), a, _ruleSource(a)));
            } 
        }

        public class FieldValidationExpression : IFieldValidationExpression
        {
            private readonly ClassValidationRules<T> _parent;
            private readonly Accessor _accessor;
            private RuleRegistrationExpression _lastRule;

            public FieldValidationExpression(ClassValidationRules<T> parent, Accessor accessor)
            {
                _parent = parent;
                _accessor = accessor;
            }

            public void If(IFieldRuleCondition condition)
            {
                _lastRule.If(condition);
            }

            public void If<TCondition>() where TCondition : IFieldRuleCondition, new()
            {
                If(new TCondition());
            }

            public void If(Func<T, bool> condition)
            {
                If(FieldRuleCondition.For(condition));
            }

            public void If(Func<T, ValidationContext, bool> condition)
            {
                If(FieldRuleCondition.For(condition));
            }

            public void IfValid()
            {
                If<IsValid>();
            }

            public FieldValidationExpression MaximumLength(int length)
            {
                return register(new MaximumLengthRule(length));
            }

            public FieldValidationExpression GreaterThanZero()
            {
                return register(new GreaterThanZeroRule());
            }

            public FieldValidationExpression GreaterOrEqualToZero()
            {
                return register(new GreaterOrEqualToZeroRule());
            }

            public FieldValidationExpression Required()
            {
                return register(new RequiredFieldRule());
            }

            public FieldValidationExpression Email()
            {
                return register(new EmailFieldRule());
            }

            public FieldValidationExpression MinimumLength(int length)
            {
                return register(new MinimumLengthRule(length));
            }

            public FieldValidationExpression MinValue(IComparable bounds)
            {
                return register(new MinValueFieldRule(bounds));
            }

            public FieldValidationExpression RangeLength(int min, int max)
            {
                return register(new RangeLengthFieldRule(min, max));
            }

            public FieldValidationExpression MaxValue(IComparable bounds)
            {
                return register(new MaxValueFieldRule(bounds));
            }

			public FieldValidationExpression RegEx(string expression)
			{
				return register(new RegularExpressionFieldRule(expression));
			}

			public FieldValidationExpression RegEx(string expression, StringToken token)
			{
				return register(new RegularExpressionFieldRule(expression, token));
			}

			public FieldEqualityRuleExpression Matches(Expression<Func<T, object>> property)
			{
				var rule = new FieldEqualityRule(_accessor, property.ToAccessor());
				_parent.Register(rule);
				return new FieldEqualityRuleExpression(rule);
			}

            private FieldValidationExpression register(IFieldValidationRule rule)
            {
                _lastRule = new RuleRegistrationExpression(a => rule, _accessor);
                _parent._rules.Add(_lastRule);

                return this;
            }

            public IFieldValidationExpression Rule(IFieldValidationRule rule)
            {
                return register(rule);
            }

            public IFieldValidationExpression Rule<TRule>() where TRule : IFieldValidationRule, new()
            {
                return Rule(new TRule());
            }
        }

		public class FieldEqualityRuleExpression
		{
			private readonly FieldEqualityRule _rule;

			public FieldEqualityRuleExpression(FieldEqualityRule rule)
			{
				_rule = rule;
			}

			public FieldEqualityRuleExpression UseToken(StringToken token)
			{
				_rule.Token = token;
				return this;
			}

			public FieldEqualityRuleExpression ReportErrorsOn(Expression<Func<T, object>> property)
			{
				_rule.ReportMessagesFor(property.ToAccessor());
				return this;
			}
		}
    }
}