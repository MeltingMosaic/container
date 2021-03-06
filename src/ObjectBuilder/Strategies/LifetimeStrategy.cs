﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.Reflection;
using Unity.Builder;
using Unity.Builder.Strategy;
using Unity.Exceptions;
using Unity.Lifetime;
using Unity.Policy;

namespace Unity.ObjectBuilder.Strategies
{
    /// <summary>
    /// An <see cref="IBuilderStrategy"/> implementation that uses
    /// a <see cref="ILifetimePolicy"/> to figure out if an object
    /// has already been created and to update or remove that
    /// object from some backing store.
    /// </summary>
    public class LifetimeStrategy : BuilderStrategy
    {
        private readonly object _genericLifetimeManagerLock = new object();

        /// <summary>
        /// Called during the chain of responsibility for a build operation. The
        /// PreBuildUp method is called when the chain is being executed in the
        /// forward direction.
        /// </summary>
        /// <param name="context">Context of the build operation.</param>
        public override void PreBuildUp(IBuilderContext context)
        {
            if (null != context.Existing) return;

            var lifetimePolicy = GetLifetimePolicy(context, out _);
            if (null == lifetimePolicy) return;

            if (lifetimePolicy is IRequiresRecovery recovery)
            {
                context.RecoveryStack.Add(recovery);
            }

            var existing = lifetimePolicy.GetValue(context.Lifetime);
            if (existing != null)
            {
                context.Existing = existing;
                context.BuildComplete = true;
            }
        }

        /// <summary>
        /// Called during the chain of responsibility for a build operation. The
        /// PostBuildUp method is called when the chain has finished the PreBuildUp
        /// phase and executes in reverse order from the PreBuildUp calls.
        /// </summary>
        /// <param name="context">Context of the build operation.</param>
        public override void PostBuildUp(IBuilderContext context)
        {
            // If we got to this method, then we know the lifetime policy didn't
            // find the object. So we go ahead and store it.
            var lifetimePolicy = GetLifetimePolicy(context, out _);
            if (null == lifetimePolicy) return;
            
            if (lifetimePolicy.GetValue() != context.Existing)
                lifetimePolicy.SetValue(context.Existing, context.Lifetime);
        }

        private ILifetimePolicy GetLifetimePolicy(IBuilderContext context, out IPolicyList source)
        {
            var policy = context.Policies.GetNoDefault<ILifetimePolicy>(context.OriginalBuildKey, false, out source);
            if (policy == null && context.OriginalBuildKey.Type.GetTypeInfo().IsGenericType)
            {
                policy = GetLifetimePolicyForGenericType(context, out source);
            }

            return policy;
        }

        private ILifetimePolicy GetLifetimePolicyForGenericType(IBuilderContext context, out IPolicyList factorySource)
        {
            var typeToBuild = context.OriginalBuildKey.Type;
            object openGenericBuildKey = new NamedTypeBuildKey(typeToBuild.GetGenericTypeDefinition(),
                                                               context.BuildKey.Name);

            var factoryPolicy = context.Policies
                                       .Get<ILifetimeFactoryPolicy>(openGenericBuildKey, out factorySource);

            if (factoryPolicy != null)
            {
                // creating the lifetime policy can result in arbitrary code execution
                // in particular it will likely result in a Resolve call, which could result in locking
                // to avoid deadlocks the new lifetime policy is created outside the lock
                // multiple instances might be created, but only one instance will be used
                ILifetimePolicy newLifetime = factoryPolicy.CreateLifetimePolicy();

                lock (_genericLifetimeManagerLock)
                {
                    // check whether the policy for closed-generic has been added since first checked
                    var lifetime = factorySource.GetNoDefault<ILifetimePolicy>(context.BuildKey);
                    if (lifetime == null && !(newLifetime is TransientLifetimeManager))
                    {
                        factorySource.Set(newLifetime, context.BuildKey);
                    }

                    lifetime = newLifetime;

                    return lifetime;
                }
            }

            return null;
        }
    }
}
