# SingleValueContainer

The SingleValueContainer class can be used to prevent duplicate instances in a .NET application. For instance, duplicate instances detected by [.NET Memory Profiler](https://memprofiler.com/features#duplicateinstances).

It provides a single copy of a requested element (based on IEqualityComparer) similar to the string.Intern method (but the container handles any class, not just strings). However, as soon as the container is cleared or GCed, the elements in the container are also eligible for collection. The SingleValueContainer is suitable to use when you have a clearly defined "region" where you want to avoid duplicate instances. This can for instance be when you open an XML-file or other document that includes a lot of duplicate string or other duplicate instances.

The WeakSingleValueContainer class is similar to the SingleValueContainer, but it only keeps a weak reference to the elements, so there's no need to explicitly clear the container. However, the overhead is significantly higher for the WeakSingleValueContainer, since a weak GC handle is created for each unique item. You can use the WeakSingleValueContainer when the "region" is not as clearly defined and/or when you expect to have many duplicates and only a few unique instances.

*NOTE!* The current implementation is a significantly refactored copy of the implementation used in .NET Memory Profiler. Unit tests have not yet been updated and are not included in this repository. The tests will be updated and included here when version 1.0 is released. 

The example below should how the SingleValueContainer can be used to prevent duplicates of double arrays.

```
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
```
