﻿using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DefaultEcs.Technical.Helper;
using DefaultEcs.Technical.Message;

namespace DefaultEcs.Technical
{
    internal sealed class ComponentPool<T>
    {
        #region Fields

        private readonly static bool _isReferenceType;
        private readonly static bool _isFlagType;

        private readonly int _worldId;
        private readonly int _maxEntityCount;

        private int[] _mapping;
        private ComponentLink[] _links;
        private T[] _components;
        private int _lastComponentIndex;

        #endregion

        #region Properties

        public int MaxComponentCount { get; }

        public bool IsNotEmpty => _lastComponentIndex > -1;

        #endregion

        #region Initialisation

        static ComponentPool()
        {
            TypeInfo typeInfo = typeof(T).GetTypeInfo();

            _isReferenceType = !typeInfo.IsValueType;
            _isFlagType = typeInfo.IsValueType && !typeInfo.IsEnum && !typeInfo.IsPrimitive && !typeInfo.DeclaredFields.Any(f => !f.IsStatic);
        }

        public ComponentPool(int worldId, int maxEntityCount, int maxComponentCount)
        {
            _worldId = worldId;
            _maxEntityCount = maxEntityCount;
            MaxComponentCount = _isFlagType ? 1 : Math.Min(maxEntityCount, maxComponentCount);

            _mapping = new int[0];
            _links = new ComponentLink[0];
            _components = new T[0];
            _lastComponentIndex = -1;

            Publisher<ComponentTypeReadMessage>.Subscribe(_worldId, On);
            Publisher<EntityDisposedMessage>.Subscribe(_worldId, On);
            Publisher<EntityCopyMessage>.Subscribe(_worldId, On);
            Publisher<ComponentReadMessage>.Subscribe(_worldId, On);
        }

        #endregion

        #region Callbacks

        private void On(in ComponentTypeReadMessage message)
        {
            message.Reader.OnRead<T>(MaxComponentCount);
        }

        private void On(in EntityDisposedMessage message) => Remove(message.EntityId);

        private void On(in EntityCopyMessage message)
        {
            if (Has(message.EntityId))
            {
                message.Copy.Set(Get(message.EntityId));
            }
        }

        private void On(in ComponentReadMessage message)
        {
            int componentIndex = message.EntityId < _mapping.Length ? _mapping[message.EntityId] : -1;
            if (componentIndex != -1)
            {
                message.Reader.OnRead(ref _components[componentIndex], new Entity(_worldId, _links[componentIndex].EntityId));
            }
        }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowMaxNumberOfComponentReached() => throw new InvalidOperationException($"Max number of component of type {nameof(T)} reached");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int entityId) => entityId < _mapping.Length && _mapping[entityId] != -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set(int entityId, in T component)
        {
            ArrayExtension.EnsureLength(ref _mapping, entityId, _maxEntityCount, -1);

            ref int componentIndex = ref _mapping[entityId];
            if (componentIndex != -1)
            {
                _components[componentIndex] = component;

                return false;
            }

            if (_lastComponentIndex == MaxComponentCount - 1)
            {
                if (_isFlagType)
                {
                    return SetSameAs(entityId, _links[_lastComponentIndex].EntityId);
                }

                ThrowMaxNumberOfComponentReached();
            }

            componentIndex = ++_lastComponentIndex;

            ArrayExtension.EnsureLength(ref _components, _lastComponentIndex, MaxComponentCount);
            ArrayExtension.EnsureLength(ref _links, _lastComponentIndex, MaxComponentCount);

            _components[_lastComponentIndex] = component;
            _links[_lastComponentIndex] = new ComponentLink(entityId);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetSameAs(int entityId, int referenceEntityId)
        {
            ArrayExtension.EnsureLength(ref _mapping, entityId, _maxEntityCount, -1);

            int referenceComponentIndex = _mapping[referenceEntityId];

            bool isNew = true;
            ref int componentIndex = ref _mapping[entityId];
            if (componentIndex != -1)
            {
                if (componentIndex == referenceComponentIndex)
                {
                    return false;
                }

                Remove(entityId);
                isNew = false;
            }

            ++_links[referenceComponentIndex].ReferenceCount;
            componentIndex = referenceComponentIndex;

            return isNew;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(int entityId)
        {
            if (entityId >= _mapping.Length)
            {
                return false;
            }

            ref int componentIndex = ref _mapping[entityId];
            if (componentIndex == -1)
            {
                return false;
            }

            ref ComponentLink link = ref _links[componentIndex];
            if (--link.ReferenceCount == 0)
            {
                if (componentIndex != _lastComponentIndex)
                {
                    ComponentLink lastLink = _links[_lastComponentIndex];
                    _links[componentIndex] = lastLink;
                    _components[componentIndex] = _components[_lastComponentIndex];
                    if (lastLink.ReferenceCount == 1)
                    {
                        _mapping[lastLink.EntityId] = componentIndex;
                    }
                    else
                    {
                        for (int i = 0; i < _mapping.Length; ++i)
                        {
                            if (_mapping[i] == _lastComponentIndex)
                            {
                                _mapping[i] = componentIndex;
                            }
                        }
                    }
                }

                if (_isReferenceType)
                {
                    _components[_lastComponentIndex] = default;
                }
                --_lastComponentIndex;
            }
            else if (link.EntityId == entityId)
            {
                int linkIndex = componentIndex;
                for (int i = 0; i < _mapping.Length; ++i)
                {
                    if (_mapping[i] == linkIndex && i != entityId)
                    {
                        link.EntityId = i;
                        break;
                    }
                }
            }

            componentIndex = -1;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(int entityId) => ref _components[_mapping[entityId]];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetAll() => new Span<T>(_components, 0, _lastComponentIndex + 1);

        #endregion
    }
}
