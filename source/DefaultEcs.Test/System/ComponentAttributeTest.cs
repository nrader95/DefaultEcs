﻿using System;
using DefaultEcs.System;
using NFluent;
using Xunit;

namespace DefaultEcs.Test.System
{
    public sealed class ComponentAttributeTest
    {
#if NET7_0_OR_GREATER
        [With<bool>]
#else
        [With(typeof(bool))]
#endif
        private sealed class WithSystem : AEntitySetSystem<int>
        {
            public WithSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

        [WithEither(typeof(bool), typeof(int))]
        [WithEither(typeof(bool), typeof(string))]
        private sealed class WithEitherSystem : AEntitySetSystem<int>
        {
            public WithEitherSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

#if NET7_0_OR_GREATER
        [With<bool>]
        [Without<string>]
#else
        [With(typeof(bool))]
        [Without(typeof(string))]
#endif
        private sealed class WithoutSystem : AEntitySetSystem<int>
        {
            public WithoutSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

#if NET7_0_OR_GREATER
        [With<bool>]
#else
        [With(typeof(bool))]
#endif
        [WithoutEither(typeof(string), typeof(char))]
        [WithoutEither(typeof(double), typeof(long))]
        private sealed class WithoutEitherSystem : AEntitySetSystem<int>
        {
            public WithoutEitherSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

#if NET7_0_OR_GREATER
        [WhenAdded<bool>]
#else
        [WhenAdded(typeof(bool))]
#endif
        private sealed class WhenAddedSystem : AEntitySetSystem<int>
        {
            public WhenAddedSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

        [WhenAddedEither(typeof(bool), typeof(int))]
        private sealed class WhenAddedEitherSystem : AEntitySetSystem<int>
        {
            public WhenAddedEitherSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

#if NET7_0_OR_GREATER
        [WhenChanged<bool>]
#else
        [WhenChanged(typeof(bool))]
#endif
        private sealed class WhenChangedSystem : AEntitySetSystem<int>
        {
            public WhenChangedSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

        [WhenChangedEither(typeof(bool), typeof(int))]
        private sealed class WhenChangedEitherSystem : AEntitySetSystem<int>
        {
            public WhenChangedEitherSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

#if NET7_0_OR_GREATER
        [With<bool>]
        [WhenRemoved<string>]
#else
        [With(typeof(bool))]
        [WhenRemoved(typeof(string))]
#endif
        private sealed class WhenRemovedSystem : AEntitySetSystem<int>
        {
            public WhenRemovedSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

#if NET7_0_OR_GREATER
        [With<bool>]
#else
        [With(typeof(bool))]
#endif
        [WhenRemovedEither(typeof(string), typeof(char))]
        private sealed class WhenRemovedEitherSystem : AEntitySetSystem<int>
        {
            public WhenRemovedEitherSystem(World world)
                : base(world)
            { }

            protected override void Update(int state, in Entity entity) => entity.Get<bool>() = true;
        }

        #region Tests

        [Fact]
        public void ComponentAttribute_Should_throw_When_componentTypes_is_null() => Check
            .ThatCode(() => new ComponentAttribute(ComponentFilterType.With, default))
            .Throws<ArgumentNullException>()
                .WithProperty(e => e.ParamName, "componentTypes");

        [Fact]
        public void WithAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WithSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();
        }

        [Fact]
        public void WithEitherAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WithEitherSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();
        }

        [Fact]
        public void WithoutAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WithoutSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();

            entity.Set<bool>();
            entity.Set<string>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();
        }

        [Fact]
        public void WithoutEitherAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WithoutEitherSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();
            entity.Set<char>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();

            entity.Set<bool>();
            entity.Set<string>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();
        }

        [Fact]
        public void WhenAddedAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WhenAddedSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();
        }

        [Fact]
        public void WhenAddedEitherAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WhenAddedEitherSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();

            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();

            entity.Set<int>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();
        }

        [Fact]
        public void WhenChangedAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WhenChangedSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();

            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();
        }

        [Fact]
        public void WhenChangedEitherAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WhenChangedEitherSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();
            entity.Set<int>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();

            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();

            entity.Get<bool>() = false;
            entity.Set<int>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();
        }

        [Fact]
        public void WhenRemovedAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WhenRemovedSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();
            entity.Set<string>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();

            entity.Remove<string>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();

            entity.Get<bool>() = false;

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();
        }

        [Fact]
        public void WhenRemovedEitherAttribute_Should_create_correct_EntitySet()
        {
            using World world = new();
            using ISystem<int> system = new WhenRemovedEitherSystem(world);

            Entity entity = world.CreateEntity();
            entity.Set<bool>();
            entity.Set<string>();
            entity.Set<char>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();

            entity.Remove<string>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();

            entity.Set<bool>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsFalse();

            entity.Remove<char>();

            system.Update(0);

            Check.That(entity.Get<bool>()).IsTrue();
        }

        #endregion
    }
}
