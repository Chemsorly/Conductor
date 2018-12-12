using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using MoreLinq;

namespace Conductor_Server.Utility
{
    //from https://github.com/CoryCharlton/CCSWE-Libraries

    [Serializable]
    [ComVisible(false)]
    [DebuggerDisplay("Count = {Count}")]
    public class AsyncObservableCollection<T> : IDisposable, IList<T>, IList, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged where T : class
    {
        bool CollectiveImplementsINotifyPropertyChanged;

        private static SynchronizationContext GetCurrentSynchronizationContext()
        {
            return SynchronizationContext.Current ?? new SynchronizationContext();
        }

        #region Constructor
        public AsyncObservableCollection() : this(new ObservableCollection<T>(), GetCurrentSynchronizationContext())
        {

        }

        public AsyncObservableCollection(ObservableCollection<T> collection) : this(collection, GetCurrentSynchronizationContext())
        {
            if (collection == null)
                throw new ArgumentNullException("collection", "'collection' cannot be null");
        }

        public AsyncObservableCollection(SynchronizationContext context) : this(new ObservableCollection<T>(), context)
        {

        }

        public AsyncObservableCollection(ObservableCollection<T> collection, SynchronizationContext context)
        {
            if (collection == null)
                throw new ArgumentNullException("collection", "'collection' cannot be null");

            if (context == null)
                throw new Exception("context cannot be null");                        

            //check if collected item implements INotifyPropertyChanged
            CollectiveImplementsINotifyPropertyChanged = typeof(T).GetInterfaces().Contains(typeof(INotifyPropertyChanged));

            _context = context;
            _items = collection;

            //BindingOperations.EnableCollectionSynchronization(this, _lockObject);

            if (CollectiveImplementsINotifyPropertyChanged)
                _items.ForEach(t => SubscribeToCollective(t));

        }

        #endregion

        #region Private Fields
        private readonly SynchronizationContext _context;
        private readonly ObservableCollection<T> _items = new ObservableCollection<T>();

        [NonSerialized]
        private Object _syncRoot;

        private readonly object _lockObject = new object();
        #endregion

        #region Events
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        public void CollectivePropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            PropertyChanged?.Invoke(sender, args);
        }

        void SubscribeToCollective(object item)
        {
            if (item != null)
            {
                INotifyPropertyChanged target = item as INotifyPropertyChanged;
                target.PropertyChanged += CollectivePropertyChanged;
            }
        }

        void UnsubscribeToCollective(object item)
        {
            if (item != null)
            {
                INotifyPropertyChanged target = item as INotifyPropertyChanged;
                target.PropertyChanged -= CollectivePropertyChanged;
            }
        }

        #region Private Properties
        bool IList.IsFixedSize
        {
            get
            {
                var list = _items as IList;
                if (list != null)
                {
                    return list.IsFixedSize;
                }

                return false;
            }
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return false; }
        }

        bool IList.IsReadOnly
        {
            get { return false; }
        }

        bool ICollection.IsSynchronized
        {
            get { return true; }
        }

        object ICollection.SyncRoot => SyncRoot;
        protected object SyncRoot
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_syncRoot == null)
                {
                    lock(_lockObject)
                    {
                        try
                        {
                            var collection = _items as ICollection;
                            if (collection != null)
                            {
                                _syncRoot = collection.SyncRoot;
                            }
                            else
                            {
                                Interlocked.CompareExchange<object>(ref _syncRoot, new object(), null);
                            }
                        }
                        catch (Exception) { }
                    }                        
                }
                return _syncRoot;
            }
        }
        #endregion

        #region Public Properties
        public int Count
        {
            get
            {
                return _items.Count;
            }
        }

        public T this[int index]
        {
            get
            {
                return _items[index];
            }
            set
            {
                T oldValue;

                lock(_lockObject)
                {
                    oldValue = this[index];

                    //change events
                    if (CollectiveImplementsINotifyPropertyChanged)
                    {
                        UnsubscribeToCollective(oldValue);
                        SubscribeToCollective(value);
                    }

                    _items[index] = value;
                    OnNotifyItemReplaced(value, oldValue, index);
                }     
            }
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set
            {
                try
                {
                    this[index] = (T)value;
                }
                catch (InvalidCastException)
                {
                    throw new ArgumentException("'value' is the wrong type");
                }
            }
        }

        #endregion

        #region Private Methods
        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>. 
            return ((value is T) || (value == null && default(T) == null));
        }

        private void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        private void OnNotifyCollectionReset()
        {
            _context.Send(state =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }, null);
        }

        private void OnNotifyItemAdded(T item, int index)
        {
            _context.Send(state =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                if(item != null && index > 0)
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
            }, null);
        }

        private void OnNotifyItemMoved(T item, int newIndex, int oldIndex)
        {
            _context.Send(state =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
            }, null);
        }

        private void OnNotifyItemRemoved(T item, int index)
        {
            _context.Send(state =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            }, null);
        }

        private void OnNotifyItemReplaced(T newItem, T oldItem, int index)
        {
            _context.Send(state =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem, index));
            }, null);
        }

        private void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged == null)
            {
                return;
            }

            _context.Send(state => propertyChanged(this, e), null);
        }
        #endregion

        #region Public Methods
        public void Add(T item)
        {
            var index = -1;

            lock (_lockObject)
            {
                index = _items.Count;
                //subscribe to events
                if (CollectiveImplementsINotifyPropertyChanged)
                    SubscribeToCollective(item);

                _items.Insert(index, item);
                OnNotifyItemAdded(item, index);
            }            
        }

        int IList.Add(object value)
        {
            int index;
            T item;

            lock (_lockObject)
            {
                index = _items.Count;
                item = (T)value;
                _items.Insert(index, (T)value);

                //subscribe to events
                if (CollectiveImplementsINotifyPropertyChanged)
                    SubscribeToCollective(value);
                OnNotifyItemAdded(item, index);
            }            
            return index;
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                //subscribe to events
                if (CollectiveImplementsINotifyPropertyChanged)
                    _items.ForEach(t => UnsubscribeToCollective(t));

                _items.Clear();
                OnNotifyCollectionReset();
            }            
        }

        public void CopyTo(T[] array, int index)
        {
            lock (_lockObject)
            {
                if (CollectiveImplementsINotifyPropertyChanged)
                    array.ForEach(t => SubscribeToCollective(t));
                //note copyto = shallow copy
                _items.CopyTo(array, index);
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array", "'array' cannot be null");
            }

            if (array.Rank != 1)
            {
                throw new ArgumentException("Multidimension arrays are not supported", "array");
            }

            if (array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException("Non-zero lower bound arrays are not supported", "array");
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("index", "'index' is out of range");
            }

            if (array.Length - index < _items.Count)
            {
                throw new ArgumentException("Array is too small");
            }

            lock(_lockObject)
            {
                var tArray = array as T[];
                if (tArray != null)
                {
                    _items.CopyTo(tArray, index);
                }
                else
                {
                    //
                    // Catch the obvious case assignment will fail.
                    // We can found all possible problems by doing the check though.
                    // For example, if the element type of the Array is derived from T,
                    // we can't figure out if we can successfully copy the element beforehand.
                    //
                    var targetType = array.GetType().GetElementType();
                    var sourceType = typeof(T);
                    if (!(targetType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(targetType)))
                    {
                        throw new ArrayTypeMismatchException("Invalid array type");
                    }

                    //
                    // We can't cast array of value type to object[], so we don't support 
                    // widening of primitive types here.
                    //
                    var objects = array as object[];
                    if (objects == null)
                    {
                        throw new ArrayTypeMismatchException("Invalid array type");
                    }

                    var count = _items.Count;
                    try
                    {
                        for (var i = 0; i < count; i++)
                        {
                            objects[index++] = _items[i];
                        }
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        throw new ArrayTypeMismatchException("Invalid array type");
                    }
                }
            }            
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        bool IList.Contains(object value)
        {
            if (IsCompatibleObject(value))
            {
                return _items.Contains((T)value);
            }

            return false;
        }

        public void Dispose()
        {
            //unsubscribe from events
            if (CollectiveImplementsINotifyPropertyChanged)
                _items.ForEach(t => UnsubscribeToCollective(t));
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_items).GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
        }

        int IList.IndexOf(object value)
        {
            if (IsCompatibleObject(value))
            {
                return _items.IndexOf((T)value);
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            lock (_lockObject)
            {
                if (CollectiveImplementsINotifyPropertyChanged)
                    SubscribeToCollective(item);

                _items.Insert(index, item);
                OnNotifyItemAdded(item, index);
            }            
        }

        void IList.Insert(int index, object value)
        {
            try
            {
                Insert(index, (T)value);
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException("'value' is the wrong type");
            }
        }

        public bool Remove(T item)
        {
            int index;
            T value;

            lock (_lockObject)
            {
                index = _items.IndexOf(item);

                if (index < 0)
                {
                    return false;
                }

                value = _items[index];

                //unsubscribe from events
                if (CollectiveImplementsINotifyPropertyChanged)
                    UnsubscribeToCollective(value);
                _items.RemoveAt(index);
                OnNotifyItemRemoved(value, index);
            }            
            return true;
        }

        void IList.Remove(object value)
        {
            if (IsCompatibleObject(value))
            {
                Remove((T)value);
            }
        }

        public void RemoveAt(int index)
        {
            T value;

            lock (_lockObject)
            {
                value = _items[index];

                //unsubscribe from events
                if (CollectiveImplementsINotifyPropertyChanged)
                    UnsubscribeToCollective(value);
                _items.RemoveAt(index);
                OnNotifyItemRemoved(value, index);
            }            
        }
        #endregion
    }
}
