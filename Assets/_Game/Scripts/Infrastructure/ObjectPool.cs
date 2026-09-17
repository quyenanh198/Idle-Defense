using System;
using System.Collections.Generic;

namespace IdleHeroDefense.Infrastructure
{
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> available = new Stack<T>();
        private readonly HashSet<T> active = new HashSet<T>();
        private readonly Func<T> factory;
        private readonly Action<T> onAcquire;
        private readonly Action<T> onRelease;
        public int ActiveCount => active.Count;
        public int AvailableCount => available.Count;

        public ObjectPool(Func<T> factory, Action<T> onAcquire = null, Action<T> onRelease = null, int initialCapacity = 0)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.onAcquire = onAcquire;
            this.onRelease = onRelease;
            for (var i = 0; i < initialCapacity; i++) available.Push(factory());
        }

        public T Acquire()
        {
            var item = available.Count > 0 ? available.Pop() : factory();
            if (!active.Add(item)) throw new InvalidOperationException("Pool item is already active.");
            onAcquire?.Invoke(item);
            return item;
        }

        public bool Release(T item)
        {
            if (item == null || !active.Remove(item)) return false;
            onRelease?.Invoke(item);
            available.Push(item);
            return true;
        }

        public void ReleaseAll()
        {
            foreach (var item in new List<T>(active)) Release(item);
        }
    }
}

