﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Unity.Builder;
using Unity.Lifetime;
using Unity.Policy;
using Unity.Registration;

namespace Unity.Container.Registration
{
    /// <summary>
    /// This class holds instance registration
    /// </summary>
    public class InstanceRegistration : IContainerRegistration, 
                                        IBuildPlanCreatorPolicy, 
                                        IBuildPlanPolicy, 
                                        IMap<Type, IBuilderPolicy>,
                                        IDisposable
    {
        #region Constructors

        /// <summary>
        /// Instance registration with the container.
        /// </summary>
        /// <remarks> <para>
        /// Instance registration is much like setting a type as a singleton, except that instead
        /// of the container creating the instance the first time it is requested, the user
        /// creates the instance ahead of type and adds that instance to the container.
        /// </para></remarks>
        /// <param name="registrationType">Type of instance to register (may be an implemented interface instead of the full type).</param>
        /// <param name="instance">Object to be returned.</param>
        /// <param name="registrationName">Name for registration.</param>
        /// <param name="lifetimeManager">
        /// <para>If null or <see cref="ContainerControlledLifetimeManager"/>, the container will take over the lifetime of the instance,
        /// calling Dispose on it (if it's <see cref="IDisposable"/>) when the container is Disposed.</para>
        /// <para>
        ///  If <see cref="ExternallyControlledLifetimeManager"/>, container will not maintain a strong reference to <paramref name="instance"/>. 
        /// User is responsible for disposing instance, and for keeping the instance typeFrom being garbage collected.</para></param>
        /// <returns>The <see cref="UnityContainer"/> object that this method was called on (this in C#, Me in Visual Basic).</returns>
        public InstanceRegistration(Type registrationType, string registrationName, object instance, LifetimeManager lifetimeManager)
        {
            // Validate input
            if (null != registrationType) InstanceIsAssignable(registrationType, instance, nameof(instance));

            Name = registrationName;
            RegisteredType = registrationType ??
                             (instance ?? throw new ArgumentNullException(nameof(instance))).GetType();

            var lifetime = lifetimeManager ?? new ContainerControlledLifetimeManager();
            if (lifetime.InUse) throw new InvalidOperationException(Constants.LifetimeManagerInUse);

            lifetime.SetValue(instance);
            LifetimeManager = lifetime;
        }

        #endregion


        #region Registry

        public IBuilderPolicy this[Type policy]
        {
            get
            {
                if (typeof(ILifetimePolicy) == policy)
                    return LifetimeManager;
                else if (typeof(IBuildKeyMappingPolicy) == policy)
                {
                    
                }
                else if (typeof(IBuildPlanCreatorPolicy) == policy)
                    return this;
                else
                {
                    Debug.WriteLine($"==== {policy} ====");
                }

                return null;
            }
            set { }
        }

        #endregion


        #region IContainerRegistration

        public string Name { get; }

        public Type RegisteredType { get; }

        public Type MappedToType { get; }

        public LifetimeManager LifetimeManager { get; }

        #endregion


        #region IBuildPlanCreatorPolicy

        public IBuildPlanPolicy CreatePlan(IBuilderContext context, NamedTypeBuildKey buildKey)
        {
            return this;
        }

        #endregion


        #region IBuildPlanPolicy

        public void BuildUp(IBuilderContext context)
        {
            context.Existing = LifetimeManager.GetValue();
            context.BuildComplete = true;
        }

        #endregion


        #region Implementation

        private static void InstanceIsAssignable(Type assignmentTargetType, object assignmentInstance, string argumentName)
        {
            if (!(assignmentTargetType ?? throw new ArgumentNullException(nameof(assignmentTargetType)))
                .GetTypeInfo().IsAssignableFrom((assignmentInstance ?? throw new ArgumentNullException(nameof(assignmentInstance))).GetType().GetTypeInfo()))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Constants.TypesAreNotAssignable,
                        assignmentTargetType, GetTypeName(assignmentInstance)),
                    argumentName);
            }
        }

        private static string GetTypeName(object assignmentInstance)
        {
            string assignmentInstanceType;
            try
            {
                assignmentInstanceType = assignmentInstance.GetType().FullName;
            }
            catch (Exception)
            {
                assignmentInstanceType = Constants.UnknownType;
            }

            return assignmentInstanceType;
        }

        #endregion


        #region IDisposable

        public void Dispose()
        {
            if (LifetimeManager is IDisposable disposable)
                disposable.Dispose();
        }

        #endregion
    }
}
