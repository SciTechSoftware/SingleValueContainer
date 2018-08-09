// -----------------------------------------------------------------------
// <copyright file="SingleValueContainerBase.cs" company="SciTech Software AB">
//     Copyright (c) SciTech Software AB. All rights reserved.  
//     Licensed under the MIT License. See LICENSE file in the project root for full license information.  
// </copyright>

namespace SciTech.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Delegate for creating an immutable key that identifies an item in 
    /// a <see cref="SingleValueContainer{T}"/> or <see cref="WeakSingleValueContainer{T}"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="workKey"></param>
    /// <returns></returns>
    public delegate T KeyCreator<T>(T workKey);

    /// <summary>
    /// Represents a container that provides a single instance of equal instances.
    /// </summary>
    /// <remarks>
    /// The container only keeps a weak reference to each unique element stored in the container. If an 
    /// element is not used elsewhere in the application domain, the element will eventually be removed 
    /// from this container.
    /// </remarks>
    /// <typeparam name="T">The type of the item in the container.</typeparam>
    /// <typeparam name="TKeyStore">The storage type of the item keys in the container. May be the same as T, or 
    /// some other storage like a weak handle.</typeparam>
	public abstract class SingleValueContainerBase<T, TKeyStore> : IEnumerable<T> where T : class
    {
        private const int InitialBucketsSize = 3;

        private static readonly int[] Primes =
        {
            3, 7, 13, 23, 41, 73, 131, 233, 409, 719, 1259, 2207, 3863, 6761, 11833, 20717,
            36263, 63463, 111091, 194413, 340237, 595451, 1042043, 1823579, 3191281,
            5584751, 9773329, 17103337, 29930851, 52379039, 91663321, 160410823,
            280718953, 491258171, 859701809, 1504478191, 2147483647
        };

        private readonly IEqualityComparer<T> comparer;

        private readonly KeyCreator<T> keyCreator;

        /// <summary>
        /// Mapping from (hashcode % buckets.Length) to slot index. The indices stored in this array is actually slotIndex+1. 
        /// This allows zero to be used as an indication of an empty (otherwise it would be necessary 
        /// to initialize all buckets to -1).
        /// </summary>
        private int[] buckets;

        /// <summary>
        /// Index of the first free (unused) slot, in a linked list of free slots. If no free
        /// slot exists, this will be -1.
        /// </summary>
        private int indexFirstFreeSlot;

        /// <summary>
        /// slots store the actual item keys, and a link to the next slot with the same (hashcode % buckets.Length). 
        /// </summary>
        private Slot[] slots;

        /// <summary>
        /// The number of slots used, either for assigned items or as part of the free slots list.
        /// </summary>
        private int usedSlotsCount;

        /// <summary>
        /// Initializes a new instance of <see cref="WeakSingleValueContainer{T}"/> class.
        /// </summary>
        /// <remarks>
        /// The optional <paramref name="keyCreator"/> is used to
        /// create an immutable key when adding new items to the container. The creator
        /// should return a key based on the added value (or the value itself, it the value is immutable). 
        /// The return key must be equal to the value and have the same hash code.
        /// </remarks>
        /// <param name="comparer">An IEqualityComparer`1{T} that is used for equality tests and hash codes.</param>
        /// <param name="keyCreator">Optional key creator.</param>
        protected SingleValueContainerBase(IEqualityComparer<T> comparer, KeyCreator<T> keyCreator)
        {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            this.keyCreator = keyCreator;
        }

        /// <summary>
        /// Gets the single instance of the provided value.
        /// </summary>
        /// <remarks>
        /// If no instance of the provided value exists in the container, it will be added and the value (or the created value key) will be returned.
        /// </remarks>
        /// <param name="value">Identifies the element to get.</param>
        /// <returns>The single instance of the element that is equal to <paramref name="value"/>.</returns>
        public T this[T value]
        {
            get
            {
                return this.Get(value, null);
            }
        }

        /// <summary>
        /// Removes all elements from this container.
        /// </summary>
        public void Clear()
        {
            for (int bucketIndex = 0; bucketIndex < this.buckets.Length; bucketIndex++)
            {
                int slotIndex = this.buckets[bucketIndex] - 1;
                while (slotIndex >= 0)
                {
                    ref var slot = ref this.slots[slotIndex];
                    this.ReleaseKey(ref slot.KeyStore);
                    slotIndex = slot.IndexNext;
                }
            }

            this.buckets = null;
            this.slots = null;
            this.usedSlotsCount = 0;
            this.indexFirstFreeSlot = -1;
        }

        /// <summary>
        /// Gets the single instance of the provided value, or adds it using the optional key creator if the value does not exist in the container.
        /// </summary>
        /// <remarks>
        /// If no instance of the provided value exists in the container, it will be added and the value (or the created value key) will be returned.
        /// </remarks>
        /// <param name="item">Identifies the element to get.</param>
        /// <param name="keyCreator">Optional key creator that overrides the one provided in the constructor.</param>
        /// <returns>The single instance of the element that is equal to <paramref name="item"/>.</returns>
        public T Get(T item, KeyCreator<T> keyCreator)
        {
            if (this.buckets == null)
            {
                this.Initialize();
            }

            // Look up a slot based on the hash code of the item
            int hashCode = this.comparer.GetHashCode(item);
            int indexBucket = GetBucketIndex(hashCode, this.buckets.Length);

            int indexPrev = -1;
            int indexSlot = this.buckets[indexBucket] - 1;
            while (indexSlot >= 0)
            {
                ref Slot slot = ref this.slots[indexSlot];

                if (this.TryGetKey(ref slot.KeyStore, out T key))
                {
                    if (slot.HashCode == hashCode &&
                        this.comparer.Equals(key, item))
                    {
                        return key;
                    }

                    indexPrev = indexSlot;
                }
                else
                {
                    // If it's not possible to get the key in this slot (e.g. a GCed weak reference),
                    // then we might as well release the slot.
                    this.ReleaseKey(ref slot.KeyStore);

                    slot.IndexNext = this.indexFirstFreeSlot;
                    this.indexFirstFreeSlot = indexSlot;

                    if (indexPrev >= 0)
                    {
                        this.slots[indexPrev].IndexNext = slot.IndexNext;
                    }
                    else
                    {
                        this.buckets[indexBucket] = slot.IndexNext + 1;
                    }
                }

                indexSlot = slot.IndexNext;
            }

            // Value not found, so it needs to be added.
            int indexNewSlot;
            if (this.indexFirstFreeSlot >= 0)
            {
                // There's a free slot we can re-use.
                indexNewSlot = this.indexFirstFreeSlot;
                this.indexFirstFreeSlot = this.slots[this.indexFirstFreeSlot].IndexNext;
            }
            else
            {
                if (this.usedSlotsCount >= this.slots.Length)
                {
                    // There's no free slot and all slots are used. Let's expand.
                    this.Expand();
                    indexBucket = GetBucketIndex(hashCode, this.buckets.Length);
                }

                indexNewSlot = this.usedSlotsCount;
                this.usedSlotsCount++;
            }

            T newKey;

            var actualKeyCreator = keyCreator ?? this.keyCreator;
            if (actualKeyCreator != null)
            {
                // Create a key value based on the provided item, with the help of the key creator.
                newKey = actualKeyCreator(item);
                if (this.comparer.GetHashCode(newKey) != hashCode
                    || !this.comparer.Equals(newKey, item))
                {
                    throw new InvalidOperationException("Created key must be equal to value and have the same hash code.");
                }
            }
            else
            {
                // No key creator available, so let's just use the provided item as the key.
                // The item value must in this case be immutable.
                newKey = item;
            }

            // Assign key to slot, and update slot hash code and bucket indices.
            ref Slot newSlot = ref this.slots[indexNewSlot];

            this.AssignKey(ref newSlot.KeyStore, newKey);

            newSlot.HashCode = hashCode;
            newSlot.IndexNext = this.buckets[indexBucket] - 1;
            this.buckets[indexBucket] = indexNewSlot + 1;

            return newKey;
        }

        /// <summary>Returns an enumerator that iterates through the collection.</summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (int bucketIndex = 0; bucketIndex < this.buckets.Length; bucketIndex++)
            {
                int slotIndex = this.buckets[bucketIndex] - 1;
                while (slotIndex >= 0)
                {
                    if (this.TryGetKey(ref this.slots[slotIndex].KeyStore, out T keyValue))
                    {
                        yield return keyValue;
                    }

                    slotIndex = this.slots[slotIndex].IndexNext;
                }
            }
        }

        /// <summary>Removes the first occurrence of a specific object from the container.</summary>
        /// <returns>true if <paramref name="item" /> was successfully removed from the container; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original container.</returns>
        /// <param name="item">The object to remove from the container.</param>
        public bool Remove(T item)
        {
            if (this.buckets == null)
            {
                return false;
            }

            int hashCode = this.comparer.GetHashCode(item);
            int indexBucket = GetBucketIndex(hashCode, this.buckets.Length);

            int indexPrevSlot = -1;
            int indexSlot = this.buckets[indexBucket] - 1;
            while (indexSlot >= 0)
            {
                ref Slot slot = ref this.slots[indexSlot];
                if (slot.HashCode == hashCode
                    && this.TryGetKey(ref slot.KeyStore, out T keyValue)
                    && this.comparer.Equals(keyValue, item))
                {
                    this.ReleaseKey(ref slot.KeyStore);

                    if (indexPrevSlot >= 0)
                    {
                        this.slots[indexPrevSlot].IndexNext = slot.IndexNext;
                    }
                    else
                    {
                        this.buckets[indexBucket] = 0;
                    }

                    slot.IndexNext = this.indexFirstFreeSlot;
                    this.indexFirstFreeSlot = indexSlot;
                    return true;
                }

                indexPrevSlot = indexSlot;
                indexSlot = slot.IndexNext;
            }

            return false;
        }

        /// <summary>
        /// Removes all elements that are no longer alive, and trims 
        /// the size of the container.
        /// </summary>
        public void TrimExcess()
        {
            if (this.buckets == null)
            {
                return;
            }

            // Start by counting the number of actually used slots.
            int count = this.CountItems();

            if (count == 0)
            {
                // Let's clear everything if the container is empty.
                this.buckets = null;
                this.slots = null;
                this.indexFirstFreeSlot = -1;
                this.usedSlotsCount = 0;

                return;
            }

            int newSize = GetNextPrime(count);

            // Create new slots and buckets (see Expand).
            Slot[] newSlots = new Slot[newSize];
            int[] newBuckets = new int[newSize];

            int newSlotIndex = 0;

            for (int oldBucketIndex = 0; oldBucketIndex < this.buckets.Length; oldBucketIndex++)
            {
                int oldSlotIndex = this.buckets[oldBucketIndex] - 1;

                while (oldSlotIndex >= 0)
                {
                    ref Slot slot = ref this.slots[oldSlotIndex];

                    // Update the oldSlotIndex before IndexNext get updated below.
                    oldSlotIndex = slot.IndexNext;

                    if (this.TryGetKey(ref slot.KeyStore, out var value))
                    {
                        int newBucketIndex = GetBucketIndex(newSlots[newSlotIndex].HashCode, newSize);
                        slot.IndexNext = newBuckets[newBucketIndex] - 1;
                        newSlots[newSlotIndex] = slot;
                        newBuckets[newBucketIndex] = newSlotIndex + 1;
                        newSlotIndex++;
                    }
                }
            }

            this.usedSlotsCount = newSlotIndex;
            this.buckets = newBuckets;
            this.slots = newSlots;
            this.indexFirstFreeSlot = -1;
        }

        /// <summary>
        /// Tries to get the single instance of the provided value.
        /// </summary>
        /// <remarks>
        /// If an instance of the provided item exists in the container, <paramref name="existingItem"/> will be initialized with this instance, 
        /// and <c>true</c> will be returned; other this method returns <c>false</c>.
        /// </remarks>
        /// <param name="item">Identifies the element to get.</param>
        /// <param name="existingItem">Initialized to the found item in the container, or default if item not found.</param>
        /// <returns><c>true</c> if an item in the container is equal to <paramref name="item"/>.</returns>
        public bool TryGet(T item, out T existingItem)
        {
            if (this.buckets != null)
            {
                int hashCode = this.comparer.GetHashCode(item);
                int indexBucket = GetBucketIndex(hashCode, this.buckets.Length);

                int indexSlot = this.buckets[indexBucket] - 1;
                while (indexSlot >= 0)
                {
                    Slot slot = this.slots[indexSlot];
                    if (slot.HashCode == hashCode
                        && this.TryGetKey(ref slot.KeyStore, out T key)
                        && this.comparer.Equals(key, item))
                    {
                        existingItem = key;
                        return true;
                    }

                    indexSlot = slot.IndexNext;
                }
            }

            existingItem = null;
            return false;
        }

        internal static int GetNextPrime(int value)
        {
            for (int primeIndex = 0; primeIndex < Primes.Length; primeIndex++)
            {
                if (Primes[primeIndex] >= value)
                {
                    return Primes[primeIndex];
                }
            }

            for (int candidate = value | 1; (uint)candidate < (uint)int.MaxValue; candidate += 2)
            {
                if (IsPrime(candidate))
                {
                    return candidate;
                }
            }

            throw new OverflowException("No prime value can be found.");
        }

        [Conditional("DEBUG")]
        internal void Validate()
        {
            int indexFree = this.indexFirstFreeSlot;

            int nFree = 0;
            while (indexFree >= 0)
            {
                nFree++;
                //Debug.Assert(!this.slots[indexFree].ValueHandle.IsAllocated, "A free entry must not be allocated");
                indexFree = this.slots[indexFree].IndexNext;
            }

            HashSet<T> values = new HashSet<T>();
            int nUsed = 0;
            int nEmpty = 0;
            for (int i = 0; i < this.buckets.Length; i++)
            {
                int index = this.buckets[i] - 1;

                while (index >= 0)
                {
                    if (this.TryGetKey(ref this.slots[index].KeyStore, out var value))
                    {
                        Debug.Assert(values.Add(value), "A duplicate value found in SharedValueProvider");

                        nUsed++;
                    }
                    else
                    {
                        nEmpty++;
                    }

                    index = this.slots[index].IndexNext;
                }
            }

            Debug.Assert(nUsed + nEmpty + nFree == this.usedSlotsCount, "SharedValueProvider entry count mismatch");
        }

        /// <summary>
        /// When implemented by a derived class, assigns the provided key to the keyStore.
        /// </summary>
        /// <param name="keyStore">Store to which the key should be assigned.</param>
        /// <param name="key">Key to assign.</param>
        protected abstract void AssignKey(ref TKeyStore keyStore, T key);

        /// <summary>
        /// When implemented by a derived class, releases a previously assigned keyStore.
        /// </summary>
        /// <param name="keyStore">The key store that should be released.</param>
        protected abstract void ReleaseKey(ref TKeyStore keyStore);

        /// <summary>
        /// When implemented by a derived class, tries to get the key from the provided key store.
        /// </summary>
        /// <param name="keyStore">Store from which the key should be retrieved.</param>
        /// <param name="value">Retrieved key, if the key is available; default otherwise</param>
        /// <returns><c>true</c> id the key is available; <c>false</c> otherwise.</returns>
        protected abstract bool TryGetKey(ref TKeyStore keyStore, out T value);

        private static int GetBucketIndex(int hashCode, int size)
        {
            return (int)((uint)hashCode % size);
        }

        private static bool IsPrime(int candidate)
        {
            if (candidate > 2 && (candidate & 1) != 0)
            {
                int num = (int)Math.Sqrt((double)candidate);
                for (int i = 3; i <= num; i += 2)
                {
                    if (candidate % i == 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            return candidate == 2;
        }

        private int CountItems()
        {
            int count = 0;
            for (int i = 0; i < this.buckets.Length; i++)
            {
                int index = this.buckets[i] - 1;

                while (index >= 0)
                {
                    ref Slot slot = ref this.slots[index];

                    if (this.TryGetKey(ref slot.KeyStore, out var _))
                    {
                        count++;
                    }

                    index = slot.IndexNext;
                }
            }

            return count;
        }

        /// <summary>
        /// Expands the slots and buckets arrays to a prime size, at least twice as big as the previous size.
        /// </summary>
        /// <remarks>Must only be called when slots are full.</remarks>
        /// <exception cref="OverflowException">Thrown if the new size would be > int.MaxValue.</exception>
        /// <exception cref="OutOfMemoryException">Thrown if new slots cannot be allocated.</exception>
        private void Expand()
        {
            Debug.Assert(this.indexFirstFreeSlot < 0 && this.usedSlotsCount >= this.slots.Length, "Expand should not be called unless slots are full.");

            // Will throw OverflowException if newSize > int.MaxValue
            int newSize = checked(this.buckets.Length * 2);

            // Make it prime.
            newSize = GetNextPrime(newSize);

            // Create new slots and buckets and copy the contents. There's actually no need
            // for the buckets and slots to be of the same size. The size of buckets should be prime to 
            // reduce the risk of collisions due to bad hash functions. It may also be larger than the slots 
            // array, to further reduce the risk of collisions (but with higher memory usage).
            Slot[] newSlots = new Slot[newSize];
            this.slots.CopyTo(newSlots, 0);

            int[] newBuckets = new int[newSize];
            for (int i = 0; i < this.usedSlotsCount; i++)
            {
                int indexBucket = GetBucketIndex(newSlots[i].HashCode, newSize);
                newSlots[i].IndexNext = newBuckets[indexBucket] - 1;
                newBuckets[indexBucket] = i + 1;
            }

            this.slots = newSlots;
            this.buckets = newBuckets;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private void Initialize()
        {
            int initialSize = GetNextPrime(InitialBucketsSize);
            this.buckets = new int[initialSize];
            this.slots = new Slot[initialSize];

            this.usedSlotsCount = 0;
            this.indexFirstFreeSlot = -1;
        }

        private struct Slot
        {
            internal int HashCode;

            internal int IndexNext;

            internal TKeyStore KeyStore;
        }
    }
}
