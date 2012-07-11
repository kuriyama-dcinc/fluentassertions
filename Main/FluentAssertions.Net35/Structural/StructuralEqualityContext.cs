using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions.Execution;
using FluentAssertions.Common;

namespace FluentAssertions.Structural
{
    internal class StructuralEqualityContext : IStructuralEqualityContext
    {
        #region Private Definitions

        private IList<object> processedObjects = new List<object>();

        #endregion

        public StructuralEqualityContext(IStructuralEqualityConfiguration config)
        {
            Config = config;
            PropertyDescription = "";
            PropertyPath = "";
        }

        public IStructuralEqualityConfiguration Config { get; private set; }

        /// <summary>
        /// Gets or sets the current property of the <see cref="Subject"/> that is being processed, or <c>null</c>
        /// if <see cref="IsRoot"/> is <c>true</c>.
        /// </summary>
        public PropertyInfo SubjectProperty { get; private set; }

        /// <summary>
        /// Gets the value of the <see cref="IStructuralEqualityContext.SubjectProperty"/>
        /// </summary>
        public object Subject { get; internal set; }

        /// <summary>
        /// Gets the property of the <see cref="IStructuralEqualityContext.Expectation"/> that was matched against the <see cref="IStructuralEqualityContext.SubjectProperty"/>, 
        /// or <c>null</c> if <see cref="IStructuralEqualityContext.IsRoot"/> is <c>true</c>.
        /// </summary>
        public PropertyInfo MatchingExpectationProperty { get; private set; }

        /// <summary>
        /// Gets the value of the <see cref="IStructuralEqualityContext.MatchingExpectationProperty"/>.
        /// </summary>
        public object Expectation { get; internal set; }

        /// <summary>
        /// Gets the full path from the root object until the current property, separated by dots.
        /// </summary>
        public string PropertyPath { get; set; }

        /// <summary>
        /// Gets a textual description of the current property based on the <see cref="PropertyPath"/>.
        /// </summary>
        public string PropertyDescription { get; private set; }

        /// <summary>
        /// A formatted phrase as is supported by <see cref="string.Format(string,object[])"/> explaining why the assertion 
        /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
        /// </summary>
        public string Reason { get; internal set; }

        /// <summary>
        /// Zero or more objects to format using the placeholders in <see cref="IStructuralEqualityContext.Reason"/>.
        /// </summary>
        public object[] ReasonArgs { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the current context represents the root of the object graph.
        /// </summary>
        public bool IsRoot
        {
            get { return (PropertyDescription.Length == 0); }
        }

        public Type CompileTimeType { get; set; }

        internal bool ContainsCyclicReference
        {
            get { return processedObjects.Contains(Subject); }
        }

        /// <summary>
        /// Gets a verification object associated with the current <see cref="IStructuralEqualityContext.Reason"/> and <see cref="IStructuralEqualityContext.ReasonArgs"/>.
        /// </summary>
        public Verification Verification
        {
            get { return Execute.Verification.BecauseOf(Reason, ReasonArgs); }
        }

        internal IEnumerable<PropertyInfo> SelectedProperties
        {
            get
            {
                IEnumerable<PropertyInfo> properties = new List<PropertyInfo>();

                foreach (ISelectionRule selectionRule in Config.SelectionRules)
                {
                    properties = selectionRule.SelectProperties(properties, new TypeInfo
                    {
                        DeclaredType = CompileTimeType,
                        RuntimeType = Subject.GetType(),
                        PropertyPath = PropertyPath,
                    });
                }

                return properties;
            }
        }

        internal void HandleCyclicReference()
        {
            if (Config.CyclicReferenceHandling == CyclicReferenceHandling.ThrowException)
            {
                Execute.Verification
                    .BecauseOf(Reason, ReasonArgs)
                    .FailWith(
                        "Expected " + PropertyDescription + " to be {0}{reason}, but it contains a cyclic reference.",
                        Expectation);
            }
        }

        internal StructuralEqualityContext CreateForNestedProperty(PropertyInfo nestedProperty)
        {
            StructuralEqualityContext nestedContext = null;

            var matchingProperty = FindMatchFor(nestedProperty);
            if (matchingProperty != null)
            {
                var subject = nestedProperty.GetValue(Subject, null);
                var expectation = matchingProperty.GetValue(Expectation, null);

                nestedContext = CreateNested(nestedProperty, subject, matchingProperty, expectation, "property ", nestedProperty.Name, ".");
            }

            return nestedContext;
        }

        private PropertyInfo FindMatchFor(PropertyInfo propertyInfo)
        {
            var query =
                from rule in Config.MatchingRules
                let match = rule.Match(propertyInfo, Expectation, PropertyDescription)
                where match != null
                select match;

            return query.FirstOrDefault();
        }

        public StructuralEqualityContext CreateForCollectionItem(int index, object subject, object expectation)
        {
            return CreateNested(SubjectProperty, subject, MatchingExpectationProperty, expectation, "item", "[" + index + "]", "");
        }

        private StructuralEqualityContext CreateNested(
            PropertyInfo subjectProperty, object subject,
            PropertyInfo matchingProperty, object expectation,
            string memberType, string memberDescription, string separator)
        {
            string propertyPath = IsRoot ? memberType : PropertyDescription + separator;

            return new StructuralEqualityContext(Config)
            {
                SubjectProperty = subjectProperty,
                MatchingExpectationProperty = matchingProperty,
                Subject = subject,
                Expectation = expectation,
                PropertyPath = PropertyPath.Combine(memberDescription),
                PropertyDescription = propertyPath + memberDescription,
                Reason = Reason,
                ReasonArgs = ReasonArgs,
                CompileTimeType = (subject != null) ? subject.GetType() : null,
                processedObjects = processedObjects.Concat(new[] {Subject}).ToList()
            };
        }
    }
}