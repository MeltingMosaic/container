// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using Unity.Builder;
using Unity.Builder.Strategy;
using Unity.Policy;

namespace Unity.ObjectBuilder.Strategies
{
    /// <summary>
    /// Represents a strategy for mapping build keys in the build up operation.
    /// </summary>
    public class BuildKeyMappingStrategy : BuilderStrategy
    {
        /// <summary>
        /// Called during the chain of responsibility for a build operation.  Looks for the <see cref="IBuildKeyMappingPolicy"/>
        /// and if found maps the build key for the current operation.
        /// </summary>
        /// <param name="context">The context for the operation.</param>
        public override void PreBuildUp(IBuilderContext context)
        {
            IBuildKeyMappingPolicy policy = (IBuildKeyMappingPolicy)context.PersistentPolicies
                                                                           .Get(typeof(IBuildKeyMappingPolicy), context.OriginalBuildKey, out _);
            if (null == policy) return;

            var existing = (policy as IDependencyResolverPolicy)?.Resolve(context);
            if (existing != null)
            {
                context.Existing = existing;
                context.BuildComplete = true;
                return;
            }

            context.BuildKey = policy.Map(context.BuildKey, context);
        }
    }
}
