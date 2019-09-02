﻿// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector.Internals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class ConditionalHashSet<T> where T : class
    {
        private const int ShrinkStepCount = 100;

        private static readonly Predicate<WeakReference> IsDead = reference => !reference.IsAlive;

        private readonly Dictionary<int, List<WeakReference>> dictionary =
            new Dictionary<int, List<WeakReference>>();

        private int shrinkCount = 0;

        internal void Add(T item)
        {
            Requires.IsNotNull(item, nameof(item));

            lock (this.dictionary)
            {
                if (this.GetWeakReferenceOrNull(item) == null)
                {
                    var weakReference = new WeakReference(item);

                    int key = weakReference.Target.GetHashCode();

                    if (!this.dictionary.TryGetValue(key, out List<WeakReference> bucket))
                    {
                        this.dictionary[key] = bucket = new List<WeakReference>(capacity: 1);
                    }

                    bucket.Add(weakReference);
                }
            }
        }

        internal void Remove(T item)
        {
            Requires.IsNotNull(item, nameof(item));

            lock (this.dictionary)
            {
                WeakReference? reference = this.GetWeakReferenceOrNull(item);

                if (reference != null)
                {
                    reference.Target = null;
                }

                if ((++this.shrinkCount % ShrinkStepCount) == 0)
                {
                    this.RemoveDeadItems();
                }
            }
        }

        internal T[] GetLivingItems()
        {
            lock (this.dictionary)
            {
                var producers =
                    from pair in this.dictionary
                    from reference in pair.Value
                    let target = reference.Target
                    where !(target is null)
                    select (T)target;

                return producers.ToArray();
            }
        }

        private WeakReference? GetWeakReferenceOrNull(T item)
        {
            if (this.dictionary.TryGetValue(item.GetHashCode(), out List<WeakReference> bucket))
            {
                foreach (var reference in bucket)
                {
                    if (object.ReferenceEquals(item, reference.Target))
                    {
                        return reference;
                    }
                }
            }

            return null;
        }

        private void RemoveDeadItems()
        {
            foreach (int key in this.dictionary.Keys.ToArray())
            {
                var bucket = this.dictionary[key];

                bucket.RemoveAll(IsDead);

                // Remove empty buckets.
                if (bucket.Count == 0)
                {
                    this.dictionary.Remove(key);
                }
            }
        }
    }
}