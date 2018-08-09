// -----------------------------------------------------------------------
// <copyright file="WeakSingleValueContainer.cs" company="SciTech Software AB">
//     Copyright (c) SciTech Software AB. All rights reserved.  
//     Licensed under the MIT License. See LICENSE file in the project root for full license information.  
// </copyright>

namespace SciTech.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a container that provides a single instance of equal instances.
    /// </summary>
    /// <remarks>
    /// The container only keeps a weak reference to each unique element stored in the container. If an 
    /// element is not used elsewhere in the application domain, the element will eventually be removed 
    /// from this container.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the container.</typeparam>
	public class WeakSingleValueContainer<T> : SingleValueContainerBase<T, GCHandle> where T : class
    {
        /// <summary>
        /// Initializes a new instance of <see cref="WeakSingleValueContainer{T}"/> class.
        /// </summary>
        /// <remarks>
        /// The optional <paramref name="keyCreator"/> is used to
        /// create an immutable key when adding new items to the container. The creator
        /// should return a key based on the added value (or the value itself, it the value is immutable). 
        /// The returned key must be equal to the value and have the same hash code.
        /// </remarks>
        /// <param name="keyCreator">Optional key creator.</param>
        public WeakSingleValueContainer(KeyCreator<T> keyCreator = null)
            : base(EqualityComparer<T>.Default, keyCreator)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="WeakSingleValueContainer{T}"/> class.
        /// </summary>
        /// <remarks>
        /// The optional <paramref name="keyCreator"/> is used to
        /// create an immutable key when adding new items to the container. The creator
        /// should return a key based on the added value (or the value itself, it the value is immutable). 
        /// The returned key must be equal to the value and have the same hash code.
        /// </remarks>
        /// <param name="comparer">An IEqualityComparer`1{T} that is used for equality tests and hash codes.</param>
        /// <param name="keyCreator">Optional key creator.</param>
        public WeakSingleValueContainer(IEqualityComparer<T> comparer, KeyCreator<T> keyCreator = null)
            : base(comparer, keyCreator)
        {
        }

        /// <summary>
        /// Releases all weak handles to items in this container.
        /// </summary>
        ~WeakSingleValueContainer()
        {
            this.Clear();
        }

        /// <summary>
        /// Assigns the provided key to the keyStore, by storing it in a weak GCHandle.
        /// </summary>
        /// <param name="keyStore">Store to which the key should be assigned.</param>
        /// <param name="key">Key to assign.</param>
        protected override void AssignKey(ref GCHandle keyStore, T key)
        {
            keyStore = GCHandle.Alloc(key);
        }

        /// <summary>
        /// Releases a previously assigned keyStore, by freeing the allocated GCHandle.
        /// </summary>
        /// <param name="keyStore">The key store that should be released.</param>
        protected override void ReleaseKey(ref GCHandle keyStore)
        {
            Debug.Assert(keyStore.IsAllocated, "ReleaseKey should only be called for assigned key stores.");
            keyStore.Free();
        }

        /// <summary>
        /// Tries to get the key from the provided key store, by retrieving the target of the GCHandle.
        /// </summary>
        /// <param name="keyStore">Store from which the key should be retrieved.</param>
        /// <param name="key">Retrieved key, if the key is available; default otherwise</param>
        /// <returns><c>true</c> id the key is available; <c>false</c> otherwise.</returns>
        protected override bool TryGetKey(ref GCHandle keyStore, out T key)
        {
            var target = keyStore.Target;
            if (target != null)
            {
                key = (T)target;
                return true;
            }

            key = default(T);
            return false;
        }
    }
}
