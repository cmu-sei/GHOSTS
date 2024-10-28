// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Domain.Code.Helpers
{
    public class LifoQueue<T> : LinkedList<T>
    {
        private readonly int _capacity;

        public LifoQueue(int capacity)
        {
            _capacity = capacity;
        }

        public void Add(T item)
        {
            if (Count > 0 && Count == _capacity) RemoveLast();
            AddFirst(item);
        }
    }
}
