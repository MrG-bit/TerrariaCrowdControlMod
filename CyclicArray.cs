///<summary>
/// File: CyclicArray.cs
/// Last Updated: 2020-07-17
/// Author: MRG-bit
/// Description: Simple class that implements a cyclic collection
///</summary>

using System;

namespace CrowdControlMod
{
    /// <summary>
    /// Array that will add and remove elements dynamically.
    /// Adding elements when the number of slots is used up will begin overwriting the elements from the beginning of the array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class CyclicArray<T>
    {
        /// <summary>
        /// Number of available slots in the array.
        /// </summary>
        public int Slots { get; private set; } = 0;

        /// <summary>
        /// Number of elements in the array.
        /// </summary>
        public int Count { get; private set; } = 0;

        private int m_index = 0;
        private readonly T[] m_data = null;

        /// <summary>
        /// Create a new array with the given number of available slots (cannot be changed later).
        /// </summary>
        /// <param name="slots">Number of available slots for elements.</param>
        public CyclicArray(int slots)
        {
            Slots = Math.Max(slots, 1);
            m_data = new T[Slots];
        }

        /// <summary>
        /// Add an element to the array.
        /// </summary>
        /// <param name="value">Element to add.</param>
        public void Add(T value)
        {
            m_data[m_index] = value;
            m_index = (m_index + 1) % Slots;
            Count = Math.Min(Count + 1, Slots);
        }

        /// <summary>
        /// Remove an element from the array.
        /// </summary>
        /// <param name="value">Element to remove.</param>
        /// <returns>True if removal succeeded.</returns>
        public bool Remove(T value)
        {
            int index = IndexOf(value);
            return (index != -1) ? RemoveAt(index) : false;
        }

        /// <summary>
        /// Remove an element from the array at a given index.
        /// </summary>
        /// <param name="index">Index of the element to remove.</param>
        /// <returns>True if removal succeeded.</returns>
        public bool RemoveAt(int index)
        {
            if (index >= 0 && index < Count)
            {
                for (int i = index; i < Count - 1; ++i)
                    m_data[i] = m_data[i + 1];
                m_index = Count - 1;
                --Count;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clear all elements from the array.
        /// </summary>
        public void Clear()
        {
            m_index = 0;
            Count = 0;
        }

        /// <summary>
        /// Check if the array contains an element.
        /// </summary>
        /// <param name="value">Element to search for.</param>
        /// <returns>True if the element exists in the array.</returns>
        public bool Contains(T value)
        {
            for (int i = 0; i < Count; ++i)
                if (m_data[i].Equals(value))
                    return true;
            return false;
        }

        /// <summary>
        /// Return the index of an element in the array.
        /// </summary>
        /// <param name="value">Element to search for.</param>
        /// <returns>Index of the element in the array (-1 if the element isn't in the array)</returns>
        public int IndexOf(T value)
        {
            for (int i = 0; i < Count; ++i)
                if (m_data[i].Equals(value))
                    return i;
            return -1;
        }

        /// <summary>
        /// Access an element at the given index in the array.
        /// </summary>
        /// <param name="index">Index of the element.</param>
        /// <returns>Element in the array.</returns>
        public T this[int index]
        {
            get
            {
                if (index >= 0 && index < Count)
                    return m_data[index];
                throw new IndexOutOfRangeException();
            }
            set
            {
                if (index >= 0 && index < Count)
                    m_data[index] = value;
                throw new IndexOutOfRangeException();
            }
        }
    }
}
