using SciTech.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace SingleValueContainerDemo
{
    internal class Program
    {
        private static void Main()
        {
            var firstArray = new double[] { 5, 10, 15, 20 };
            var secondArray = new double[] { 5, 10, 15, 20 };

            bool areSame = ReferenceEquals(firstArray, secondArray);
            Console.WriteLine(areSame ? "firstArray and secondArray are the same" : "firstArray and secondArray are NOT the same");

            // Create a single value container that will provide single instances of read-only double lists (IReadOnlyList<double>). 
            // A comparer is needed to compare the contents of the lists (rather than the list references).
            // A key creator is provided to make sure that the lists stored in the container are actually immutable.
            var container = new SingleValueContainer<IReadOnlyList<double>>(
                ListEqualityComparer<double>.Default,
                item => item as IImmutableList<double> ?? ImmutableArray.CreateRange(item));

            var firstSingleArray = container[firstArray];
            Debug.Assert(!ReferenceEquals(firstArray, firstSingleArray), "firstArray should not be same as firstSingleArray, since it is not immutable.");

            var secondSingleArray = container[secondArray];

            bool areSingleSame = ReferenceEquals(firstSingleArray, secondSingleArray);
            Console.WriteLine(areSingleSame ? "firstSingleArray and secondSingleArray are the same" : "firstSingleArray and secondSingleArray are NOT the same");

            Console.ReadLine();
        }
    }

    internal class ListEqualityComparer<T> : IEqualityComparer<IReadOnlyList<T>>
    {
        public static readonly ListEqualityComparer<T> Default = new ListEqualityComparer<T>();

        public bool Equals(IReadOnlyList<T> x, IReadOnlyList<T> y)
        {
            return x == y || (x != null && y != null && x.SequenceEqual(y));
        }

        public int GetHashCode(IReadOnlyList<T> list)
        {
            int hashCode = 0;
            if (list != null)
            {
                foreach (var item in list)
                {
                    hashCode += item?.GetHashCode() ?? 0;
                }
            }

            return hashCode;
        }
    }
}