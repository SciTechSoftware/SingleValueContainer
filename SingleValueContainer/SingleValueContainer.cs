// -----------------------------------------------------------------------
// <copyright file="SingleValueContainer.cs" company="SciTech Software AB">
//     Copyright (c) SciTech Software AB. All rights reserved.  
//     Licensed under the MIT License. See LICENSE file in the project root for full license information.  
// </copyright>

namespace SciTech.Collections
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Represents a container that provides a single instance of equal instances.
    /// </summary>
    /// <remarks>
    /// The container keeps a strong reference to each unique item stored in the container. To allow
    /// the items to be garbage collected, <see cref="SingleValueContainerBase{T,TKeyStore}.Clear"/> the container or allow the container 
    /// itself to be garbage collected.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the container.</typeparam>
	public class SingleValueContainer<T> : SingleValueContainerBase<T,T> where T : class
    {
        private int count;


        /// <summary>
        /// Initializes a new instance of <see cref="SingleValueContainer{T}"/> class.
        /// </summary>
        /// <remarks>
        /// The optional <paramref name="keyCreator"/> is used to
        /// create an immutable key when adding new items to the container. The creator
        /// should return a key based on the added value (or the value itself, if it is immutable). 
        /// The return key must be equal to the value and have the same hash code.
        /// </remarks>
        /// <param name="keyCreator">Optional key creator.</param>
        public SingleValueContainer(KeyCreator<T> keyCreator = null)
            : base(null, keyCreator)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SingleValueContainer{T}"/> class.
        /// </summary>
        /// <remarks>
        /// The optional <paramref name="keyCreator"/> is used to
        /// create an immutable key when adding new items to the container. The creator
        /// should return a key based on the added value (or the value itself, if it is immutable). 
        /// The return key must be equal to the value and have the same hash code.
        /// </remarks>
        /// <param name="comparer">An IEqualityComparer`1{T} that is used for equality tests and hash codes.</param>
        /// <param name="keyCreator">Optional key creator.</param>
        public SingleValueContainer(IEqualityComparer<T> comparer, KeyCreator <T> keyCreator=null ) : base( comparer, keyCreator )
        {
        }

        /// <summary>Gets the number of elements contained in the container.</summary>
        /// <returns>The number of elements contained in the container.</returns>
        public int Count
        {
            get { return this.count; }
        }

        /// <summary>Gets a value indicating whether the container is read-only. This will always be <c>false</c>.</summary>
        /// <returns><c>false</c>, since the container is never read-only.</returns>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>Adds an item to the container, unless an equal item is already added.</summary>
        /// <param name="item">The object to add to the container.</param>
        public void Add(T item)
        {
            this.Get(item, null);
        }

        /// <summary>Determines whether the container contains a specific item.</summary>
        /// <returns>true if <paramref name="item" /> is found in the container; otherwise, false.</returns>
        /// <param name="item">The object to locate in the container.</param>
        public bool Contains(T item)
        {
            return this.TryGet(item, out T existingItem);
        }

        /// <summary>Copies the elements of the container to an <see cref="System.Array" />, starting at a particular <see cref="System.Array" /> index.</summary>
        /// <param name="array">The one-dimensional <see cref="System.Array" /> that is the destination of the elements copied from container. The <see cref="System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="array" /> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="arrayIndex" /> is less than 0.</exception>
        /// <exception cref="System.ArgumentException">The number of elements in the source container is greater than the available space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Assigns the provided key to the keyStore, by direct assignment (and updates the count of items in the container).
        /// </summary>
        /// <param name="keyStore">Store to which the key should be assigned.</param>
        /// <param name="key">Key to assign.</param>
        protected override void AssignKey(ref T keyStore, T key)
        {
            keyStore = key;
            this.count++;
        }

        /// <summary>
        /// Releases a previously assigned keyStore by clearing the store (and updated the count of items in the container).
        /// </summary>
        /// <param name="keyStore">The key store that should be released.</param>
        protected override void ReleaseKey(ref T keyStore)
        {
            keyStore = default(T);
            this.count--;
        }


        /// <summary>
        /// Tries to get the key from the provided key store, by simply returning the key store value.
        /// </summary>
        /// <param name="keyStore">Store from which the key should be retrieved.</param>
        /// <param name="value">Retrieved key, if the key is available; default otherwise</param>
        /// <returns><c>true</c> id the key is available; <c>false</c> otherwise.</returns>
        protected override bool TryGetKey(ref T keyStore, out T value)
        {
            value = keyStore;
            return true;
        }

    }
}
