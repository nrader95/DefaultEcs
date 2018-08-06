﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace DefaultEcs.Serialization
{
    /// <summary>
    /// Provides a basic implementation of the <see cref="ISerializer"/> interface using a text readable format.
    /// </summary>
    public sealed class TextSerializer : ISerializer
    {
        #region Types

        private static class Converter<T>
        {
            #region Types

            private delegate void WriteAction(in T value, StreamWriter writer, int indentation);
            private delegate T ReadAction(string line, StreamReader reader);
            private delegate void ReadFieldAction(string line, StreamReader reader, ref T value);

            #endregion

            #region Fields

            private static readonly Dictionary<string, ReadFieldAction> _readFieldActions;
            private static readonly WriteAction _writeAction;
            private static readonly ReadAction _readAction;

            #endregion

            #region Initialisation

            static Converter()
            {
                #region Filters

                bool Parameters_string(MethodInfo methodInfo)
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();

                    return parameters.Length == 1
                        && parameters[0].ParameterType == typeof(string);
                }

                bool Parameters_string_string(MethodInfo methodInfo)
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();

                    return parameters.Length == 2
                        && parameters[0].ParameterType == typeof(string)
                        && parameters[1].ParameterType == typeof(string);
                }

                #endregion

                _readFieldActions = new Dictionary<string, ReadFieldAction>();

                ParameterExpression value = Expression.Parameter(typeof(T).MakeByRefType());
                ParameterExpression writer = Expression.Parameter(typeof(StreamWriter));
                ParameterExpression indentation = Expression.Parameter(typeof(int));

                MethodInfo write = typeof(StreamWriter).GetTypeInfo().GetDeclaredMethods(nameof(StreamWriter.Write)).First(Parameters_string);
                MethodInfo writeLineString = typeof(StreamWriter).GetTypeInfo().GetDeclaredMethods(nameof(StreamWriter.WriteLine)).First(Parameters_string);
                MethodInfo stringConcat = typeof(string).GetTypeInfo().GetDeclaredMethods(nameof(string.Concat)).First(Parameters_string_string);

                ParameterExpression line = Expression.Parameter(typeof(string));
                ParameterExpression reader = Expression.Parameter(typeof(StreamReader));

                Expression readIfNull = Expression.Condition(
                    Expression.Equal(line, Expression.Constant(null, typeof(string))),
                    Expression.Call(reader, typeof(StreamReader).GetTypeInfo().GetDeclaredMethod(nameof(StreamReader.ReadLine))),
                    line);

                Expression writeBody = Expression.Call(writer, writeLineString, Expression.Call(value, typeof(object).GetTypeInfo().GetDeclaredMethod(nameof(object.ToString))));
                Expression readBody = null;

                #region clr types
                if (typeof(T) == typeof(bool))
                {
                    readBody = Expression.Call(typeof(bool).GetTypeInfo().GetDeclaredMethods(nameof(bool.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    bool Is_SByte_Parse(MethodInfo methodInfo)
                    {
                        ParameterInfo[] parameters = methodInfo.GetParameters();

                        return parameters.Length == 1
                            && parameters[0].ParameterType == typeof(string);
                    }

                    readBody = Expression.Call(typeof(sbyte).GetTypeInfo().GetDeclaredMethods(nameof(sbyte.Parse)).First(Is_SByte_Parse), readIfNull);
                }
                else if (typeof(T) == typeof(byte))
                {
                    readBody = Expression.Call(typeof(byte).GetTypeInfo().GetDeclaredMethods(nameof(byte.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(short))
                {
                    readBody = Expression.Call(typeof(short).GetTypeInfo().GetDeclaredMethods(nameof(short.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(ushort))
                {
                    readBody = Expression.Call(typeof(ushort).GetTypeInfo().GetDeclaredMethods(nameof(ushort.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(int))
                {
                    readBody = Expression.Call(typeof(int).GetTypeInfo().GetDeclaredMethods(nameof(int.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(uint))
                {
                    readBody = Expression.Call(typeof(uint).GetTypeInfo().GetDeclaredMethods(nameof(uint.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(long))
                {
                    readBody = Expression.Call(typeof(long).GetTypeInfo().GetDeclaredMethods(nameof(long.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(ulong))
                {
                    readBody = Expression.Call(typeof(ulong).GetTypeInfo().GetDeclaredMethods(nameof(ulong.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(char))
                {
                    readBody = Expression.Call(typeof(Converter<T>).GetTypeInfo().GetDeclaredMethod(nameof(CharParse)), readIfNull);
                }
                else if (typeof(T) == typeof(decimal))
                {
                    readBody = Expression.Call(typeof(decimal).GetTypeInfo().GetDeclaredMethods(nameof(decimal.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(double))
                {
                    readBody = Expression.Call(typeof(double).GetTypeInfo().GetDeclaredMethods(nameof(double.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(float))
                {
                    readBody = Expression.Call(typeof(float).GetTypeInfo().GetDeclaredMethods(nameof(float.Parse)).First(Parameters_string), readIfNull);
                }
                else if (typeof(T) == typeof(string))
                {
                    readBody = readIfNull;
                }
                else if (typeof(T).GetTypeInfo().IsEnum)
                {
                    readBody = Expression.Call(typeof(Converter<T>).GetTypeInfo().GetDeclaredMethod(nameof(ReadEnum)).MakeGenericMethod(typeof(T)), readIfNull);
                }
                else
                #endregion
                {
                    ParameterExpression space = Expression.Variable(typeof(string));
                    Expression assignSpace = Expression.Assign(space, Expression.Call(typeof(Converter<T>).GetTypeInfo().GetDeclaredMethod(nameof(CreateIndentation)), indentation));

                    List<Expression> writeExpressions = new List<Expression>
                    {
                        Expression.Call(writer, writeLineString, Expression.Constant(_objectBegin)),
                        Expression.PreIncrementAssign(indentation),
                        assignSpace
                    };

                    foreach (FieldInfo fieldInfo in typeof(T).GetTypeInfo().DeclaredFields.Where(f => !f.IsStatic))
                    {
                        string name = GetFriendlyName(fieldInfo.Name);
                        writeExpressions.Add(Expression.Call(writer, write, Expression.Call(stringConcat, space, Expression.Constant(name))));
                        writeExpressions.Add(Expression.Call(writer, write, Expression.Constant(" ")));
                        writeExpressions.Add(Expression.Call(
                            typeof(Converter<>).MakeGenericType(fieldInfo.FieldType).GetTypeInfo().GetDeclaredMethod(nameof(Write)),
                            Expression.Field(value, fieldInfo),
                            writer,
                            indentation));

                        DynamicMethod dynMethod = new DynamicMethod($"Set_{nameof(T)}_{fieldInfo.Name}", typeof(void), new[] { typeof(string), typeof(StreamReader), typeof(T).MakeByRefType() }, typeof(Converter<T>), true);
                        ILGenerator generator = dynMethod.GetILGenerator();
                        generator.Emit(OpCodes.Ldarg_2);
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldarg_1);
                        generator.Emit(OpCodes.Call, typeof(Converter<>).MakeGenericType(fieldInfo.FieldType).GetTypeInfo().GetDeclaredMethod(nameof(Converter<T>.Read)));
                        generator.Emit(OpCodes.Stfld, fieldInfo);
                        generator.Emit(OpCodes.Ret);

                        _readFieldActions.Add(name.ToUpperInvariant(), (ReadFieldAction)dynMethod.CreateDelegate(typeof(ReadFieldAction)));
                    }

                    writeExpressions.Add(Expression.PreDecrementAssign(indentation));
                    writeExpressions.Add(assignSpace);
                    writeExpressions.Add(Expression.Call(writer, writeLineString, Expression.Call(stringConcat, space, Expression.Constant(_objectEnd))));

                    writeBody = Expression.Block(new[] { space }, writeExpressions);
                    _readAction = ReadAnyType;
                }

                _writeAction = Expression.Lambda<WriteAction>(writeBody, value, writer, indentation).Compile();
                if (readBody != null)
                {
                    _readAction = Expression.Lambda<ReadAction>(readBody, line, reader).Compile();
                }
            }

            #endregion

            #region Methods

            public static char CharParse(string entry)
            {
                if (!char.TryParse(entry, out char result))
                {
                    throw new ArgumentException($"Could not convert '{entry}' to a char");
                }

                return result;
            }

            public static TEnum ReadEnum<TEnum>(string entry)
                where TEnum : struct
            {
                return Enum.TryParse(entry, out TEnum value) ? value : throw new ArgumentException($"Unable to convert '{entry}' to enum type {typeof(TEnum).AssemblyQualifiedName}");
            }

            public static string GetFriendlyName(string name)
            {
                Match match = Regex.Match(name, "^<(.+)>k__BackingField$");
                if (match.Success)
                {
                    name = match.Groups[1].Value;
                }
                return name.Trim('_');
            }

            public static string CreateIndentation(int indentation) => string.Join(string.Empty, Enumerable.Repeat(_indentation, indentation));

            private static T ReadAnyType(string line, StreamReader reader)
            {
                // handle ref type
                T value = default;

                while ((string.IsNullOrWhiteSpace(line) || line.Split(_space, StringSplitOptions.RemoveEmptyEntries)[0] != _objectBegin) && !reader.EndOfStream)
                {
                    line = reader.ReadLine();
                }

                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine();
                    string[] parts = line.Split(_space, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        if (parts[0] == _objectEnd)
                        {
                            break;
                        }
                        else if (_readFieldActions.TryGetValue(parts[0].ToUpperInvariant(), out ReadFieldAction action))
                        {
                            action(parts.Length > 1 ? parts[1] : null, reader, ref value);
                        }
                    }
                }

                return value;
            }

            public static void Write(in T value, StreamWriter writer, int indentation) => _writeAction(value, writer, indentation);

            public static T Read(string line, StreamReader reader) => _readAction(line, reader);

            #endregion
        }

        private sealed class ComponentTypeWriter : IComponentTypeReader
        {
            #region Fields

            private readonly StreamWriter _writer;
            private readonly Dictionary<Type, string> _types;
            private readonly int _maxEntityCount;

            #endregion

            #region Initialisation

            public ComponentTypeWriter(StreamWriter writer, Dictionary<Type, string> types, int maxEntityCount)
            {
                _writer = writer;
                _types = types;
                _maxEntityCount = maxEntityCount;
            }

            #endregion

            #region IComponentTypeReader

            public void OnRead<T>(int maxComponentCount)
            {
                string shortName = typeof(T).Name;

                int repeatCount = 1;
                while (_types.ContainsValue(shortName))
                {
                    shortName = $"{typeof(T).Name}_{repeatCount}";
                }

                _types.Add(typeof(T), shortName);

                _writer.WriteLine($"{_componentType} {shortName} {typeof(T).AssemblyQualifiedName}");
                if (maxComponentCount != _maxEntityCount)
                {
                    _writer.WriteLine($"{_maxComponentCount} {shortName} {maxComponentCount}");
                }
            }

            #endregion
        }

        private sealed class ComponentWriter : IComponentReader
        {
            #region Fields

            private readonly StreamWriter _writer;
            private readonly Dictionary<Type, string> _types;
            private readonly Dictionary<Entity, int> _entities;
            private readonly Dictionary<Tuple<Entity, Type>, int> _components;

            private int _entityCount;
            private Entity _currentEntity;

            #endregion

            #region Initialisation

            public ComponentWriter(StreamWriter writer, Dictionary<Type, string> types)
            {
                _writer = writer;
                _types = types;
                _entities = new Dictionary<Entity, int>();
                _components = new Dictionary<Tuple<Entity, Type>, int>();
            }

            #endregion

            #region Methods

            public void WriteEntity(in Entity entity)
            {
                _entities.Add(entity, ++_entityCount);
                _currentEntity = entity;

                _writer.WriteLine();
                _writer.WriteLine($"{_entity} {_entityCount}");

                entity.ReadAllComponents(this);
            }

            #endregion

            #region IComponentReader

            public void OnRead<T>(ref T component, in Entity componentOwner)
            {
                Tuple<Entity, Type> componentKey = Tuple.Create(componentOwner, typeof(T));
                if (_components.TryGetValue(componentKey, out int key))
                {
                    _writer.WriteLine($"{_componentSameAs} {_types[typeof(T)]} {key}");
                }
                else
                {
                    _components.Add(componentKey, _entityCount);
                    _writer.Write($"{_component} {_types[typeof(T)]} ");
                    Converter<T>.Write(component, _writer, 0);
                }
            }

            #endregion
        }

        private interface IOperation
        {
            void SetMaximumComponentCount(World world, int maxComponentCount);
            void SetComponent(string line, StreamReader reader, in Entity entity);
            void SetSameAsComponent(in Entity entity, in Entity reference);
        }

        private sealed class Operation<T> : IOperation
        {
            #region IOperation

            public void SetMaximumComponentCount(World world, int maxComponentCount) => world.SetMaximumComponentCount<T>(maxComponentCount);

            public void SetComponent(string line, StreamReader reader, in Entity entity)
            {
                try
                {
                    entity.Set(Converter<T>.Read(line, reader));
                }
                catch (Exception exception)
                {
                    throw new ArgumentException("Error while parsing", exception);
                }
            }

            public void SetSameAsComponent(in Entity entity, in Entity reference) => entity.SetSameAs<T>(reference);

            #endregion
        }

        #endregion

        #region Fields

        private const string _maxEntityCount = "MaxEntityCount";
        private const string _componentType = "ComponentType";
        private const string _maxComponentCount = "MaxComponentCount";
        private const string _entity = "Entity";
        private const string _component = "Component";
        private const string _componentSameAs = "ComponentSameAs";
        private const string _objectBegin = "{";
        private const string _objectEnd = "}";
        private const string _indentation = "    ";

        private static readonly char[] _space = new[] { ' ' };
        private static readonly ConcurrentDictionary<Type, IOperation> _operations = new ConcurrentDictionary<Type, IOperation>();

        #endregion

        #region Methods

        private static World CreateWorld(StreamReader reader)
        {
            World world = null;
            do
            {
                string[] lineParts = reader.ReadLine()?.Split(_space, StringSplitOptions.RemoveEmptyEntries);
                if (lineParts?.Length > 1
                    && _maxEntityCount.Equals(lineParts[0]))
                {
                    if (!int.TryParse(lineParts[1], out int maxEntityCount))
                    {
                        throw new ArgumentException($"Unable to convert '{lineParts[1]}' to a number");
                    }

                    world = new World(maxEntityCount);
                    break;
                }
            }
            while (!reader.EndOfStream);

            return world;
        }

        private static IOperation CreateOperation(Type type) => (IOperation)Activator.CreateInstance(typeof(Operation<>).MakeGenericType(type));

        private static void CreateOperation(string entry, Dictionary<string, IOperation> operations)
        {
            string[] componentTypeEntry = entry.Split(_space, 2, StringSplitOptions.RemoveEmptyEntries);
            Type type = Type.GetType(componentTypeEntry[1], false) ?? throw new ArgumentException($"Unable to get type from '{componentTypeEntry[1]}'");

            operations.Add(componentTypeEntry[0], _operations.GetOrAdd(type, CreateOperation));
        }

        private static void SetMaxComponentCount(World world, string entry, Dictionary<string, IOperation> operations)
        {
            string[] maxComponentCountEntry = entry.Split(_space, StringSplitOptions.RemoveEmptyEntries);
            if (maxComponentCountEntry.Length < 2)
            {
                throw new ArgumentException($"Unable to get {_maxComponentCount} information from '{entry}'");
            }
            if (!operations.TryGetValue(maxComponentCountEntry[0], out IOperation operation))
            {
                throw new ArgumentException($"Unknown component type used '{maxComponentCountEntry[0]}'");
            }
            if (!int.TryParse(maxComponentCountEntry[1], out int maxComponentCount))
            {
                throw new ArgumentException($"Unable to convert '{maxComponentCountEntry[1]}' to a number");
            }

            operation.SetMaximumComponentCount(world, maxComponentCount);
        }

        private static void SetComponent(StreamReader reader, in Entity entity, string entry, Dictionary<string, IOperation> operations)
        {
            if (entity.Equals(default))
            {
                throw new ArgumentException($"Encountered a component before creation of an Entity");
            }
            string[] componentEntry = entry.Split(_space, 2, StringSplitOptions.RemoveEmptyEntries);
            if (componentEntry.Length < 1)
            {
                throw new ArgumentException($"Unable to get component type information from '{entry}'");
            }
            if (!operations.TryGetValue(componentEntry[0], out IOperation operation))
            {
                throw new ArgumentException($"Unknown component type used '{componentEntry[0]}'");
            }

            operation.SetComponent(componentEntry.Length > 1 ? componentEntry[1] : null, reader, entity);
        }

        private static void SetSameAsComponent(in Entity entity, string entry, Dictionary<string, Entity> entities, Dictionary<string, IOperation> operations)
        {
            if (entity.Equals(default))
            {
                throw new ArgumentException($"Encountered a component before creation of an Entity");
            }
            string[] componentEntry = entry.Split(_space, StringSplitOptions.RemoveEmptyEntries);
            if (componentEntry.Length < 2)
            {
                throw new ArgumentException($"Unable to get component type information from '{entry}'");
            }
            if (!operations.TryGetValue(componentEntry[0], out IOperation operation))
            {
                throw new ArgumentException($"Unknown component type used '{componentEntry[0]}'");
            }
            if (!entities.TryGetValue(componentEntry[1], out Entity reference))
            {
                throw new ArgumentException($"Unknown reference Entity '{componentEntry[1]}'");
            }

            operation.SetSameAsComponent(entity, reference);
        }

        #endregion

        #region ISerializer

        /// <summary>
        /// Serializes the given <see cref="World"/> into the provided <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> in which the data will be saved.</param>
        /// <param name="world">The <see cref="World"/> instance to save.</param>
        public void Serialize(Stream stream, World world)
        {
            stream = stream ?? throw new ArgumentNullException(nameof(stream));
            world = world ?? throw new ArgumentNullException(nameof(world));

            using (StreamWriter writer = new StreamWriter(stream))
            {
                Dictionary<Type, string> types = new Dictionary<Type, string>();

                writer.WriteLine($"{_maxEntityCount} {world.MaxEntityCount}");
                world.ReadAllComponentTypes(new ComponentTypeWriter(writer, types, world.MaxEntityCount));

                ComponentWriter componentReader = new ComponentWriter(writer, types);

                foreach (Entity entity in world.GetAllEntities())
                {
                    componentReader.WriteEntity(entity);
                }
            }
        }

        /// <summary>
        /// Deserializes a <see cref="World"/> instance from the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> from which the data will be loaded.</param>
        /// <returns>The <see cref="World"/> instance loaded.</returns>
        public World Deserialize(Stream stream)
        {
            stream = stream ?? throw new ArgumentNullException(nameof(stream));

            using (StreamReader reader = new StreamReader(stream))
            {
                World world = CreateWorld(reader) ?? throw new ArgumentException("Could not create a World instance from the provided Stream");
                Entity currentEntity = default;
                Dictionary<string, Entity> entities = new Dictionary<string, Entity>();
                Dictionary<string, IOperation> operations = new Dictionary<string, IOperation>();

                do
                {
                    string[] lineParts = reader.ReadLine()?.Split(_space, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (lineParts.Length > 0)
                    {
                        string entry = lineParts[0];
                        if (_entity.Equals(entry))
                        {
                            currentEntity = world.CreateEntity();
                            if (lineParts.Length > 1)
                            {
                                entities.Add(lineParts[1], currentEntity);
                            }
                        }
                        else if (lineParts.Length > 1)
                        {
                            if (_componentType.Equals(entry))
                            {
                                CreateOperation(lineParts[1], operations);
                            }
                            else if (_maxComponentCount.Equals(entry))
                            {
                                SetMaxComponentCount(world, lineParts[1], operations);
                            }
                            else if (_component.Equals(entry))
                            {
                                SetComponent(reader, currentEntity, lineParts[1], operations);
                            }
                            else if (_componentSameAs.Equals(entry))
                            {
                                SetSameAsComponent(currentEntity, lineParts[1], entities, operations);
                            }
                        }
                    }
                }
                while (!reader.EndOfStream);

                return world;
            }
        }

        #endregion
    }
}
