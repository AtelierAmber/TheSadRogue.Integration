﻿// SadConsole does not use nullable reference types and for all intents and purposes this class is a copy-paste
// with trivial modification; so we disable nullable reference types for it to ensure consistent interfaces.
#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Serialization;
using SadConsole;
using SadConsole.Components;
using SadConsole.Input;
using SadRogue.Primitives;

namespace SadRogue.Integration.Rendering
{
    /// <summary>
    /// A version of SadConsole's ScreenObject suitable for use as a backing field for an interface implementation.
    /// Meant to be used as the <see cref="Maps.RogueLikeMapBase.BackingObject"/> field for a map implementation.
    /// </summary>
    [DataContract]
    public class MapScreenObject : IScreenObject
    {
        [DataMember(Name = "Children")]
        private IScreenObject[] _childrenSerialized;

        [DataMember(Name = "Components")]
        private IComponent[] _componentsSerialized;

        [DataMember(Name = "Position")]
        private Point _position;

        private IScreenObject _parentObject;
        private bool _isVisible = true;
        private bool _isEnabled = true;
        private bool _isfocused;


        /// <inheritdoc/>
        public event EventHandler<ValueChangedEventArgs<IScreenObject>> ParentChanged;

        /// <inheritdoc/>
        public event EventHandler<ValueChangedEventArgs<Point>> PositionChanged;

        /// <inheritdoc/>
        public event EventHandler VisibleChanged;

        /// <inheritdoc/>
        public event EventHandler EnabledChanged;

        /// <inheritdoc/>
        public event EventHandler FocusLost;

        /// <inheritdoc/>
        public event EventHandler Focused;

        /// <summary>
        /// A filtered list from <see cref="SadComponents"/> where <see cref="IComponent.IsUpdate"/> is <see langword="true"/>.
        /// </summary>
        protected List<IComponent> ComponentsUpdate;

        /// <summary>
        /// A filtered list from <see cref="SadComponents"/> where <see cref="IComponent.IsRender"/> is <see langword="true"/>.
        /// </summary>
        protected List<IComponent> ComponentsRender;

        /// <summary>
        /// A filtered list from <see cref="SadComponents"/> where <see cref="IComponent.IsMouse"/> is <see langword="true"/>.
        /// </summary>
        protected List<IComponent> ComponentsMouse;

        /// <summary>
        /// A filtered list from <see cref="SadComponents"/> where <see cref="IComponent.IsKeyboard"/> is <see langword="true"/>.
        /// </summary>
        protected List<IComponent> ComponentsKeyboard;

        /// <summary>
        /// A filtered list from <see cref="SadComponents"/> that is not set for update, render, mouse, or keyboard.
        /// </summary>
        protected List<IComponent> ComponentsEmpty;

        /// <inheritdoc/>
        public ObservableCollection<IComponent> SadComponents { get; protected set; }

        /// <inheritdoc/>
        public ScreenObjectCollection Children { get; protected set; }

        /// <inheritdoc/>
        public IScreenObject Parent
        {
            get => _parentObject;
            set
            {
                if (value == OwningObject) throw new Exception("Cannot set parent to itself.");
                if (_parentObject == value) return;

                if (_parentObject == null)
                {
                    _parentObject = value;
                    _parentObject.Children.Add(OwningObject);
                    OnParentChanged(null, _parentObject);
                }
                else
                {
                    IScreenObject oldParent = _parentObject;
                    _parentObject = null;
                    oldParent.Children.Remove(OwningObject);
                    _parentObject = value;

                    _parentObject?.Children.Add(OwningObject);
                    OnParentChanged(oldParent, _parentObject);
                }
            }
        }

        /// <inheritdoc/>
        public Point Position
        {
            get => _position;
            set
            {
                if (_position == value) return;

                Point oldPoint = _position;
                _position = value;
                OnPositionChanged(oldPoint, _position);
            }
        }

        /// <inheritdoc/>
        public Point AbsolutePosition { get; protected set; }

        /// <inheritdoc/>
        [DataMember]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;

                _isVisible = value;
                OnVisibleChanged();
            }
        }

        /// <inheritdoc/>
        [DataMember]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;

                _isEnabled = value;
                OnEnabledChanged();
            }
        }

        /// <inheritdoc/>
        public bool IsFocused
        {
            get => _isfocused;
            set
            {
                if ((_isfocused && value) || (!_isfocused && !value)) return;

                _isfocused = value;

                if (value)
                {
                    switch (FocusedMode)
                    {
                        case FocusBehavior.Set:
                            GameHost.Instance.FocusedScreenObjects.Set(OwningObject);
                            break;
                        case FocusBehavior.Push:
                            GameHost.Instance.FocusedScreenObjects.Push(OwningObject);
                            break;
                    }

                    Focused?.Invoke(OwningObject, EventArgs.Empty);
                    OnFocused();
                }
                else
                {
                    if (GameHost.Instance.FocusedScreenObjects.ScreenObject == OwningObject && FocusedMode != FocusBehavior.None)
                        GameHost.Instance.FocusedScreenObjects.Pop(OwningObject);

                    FocusLost?.Invoke(OwningObject, EventArgs.Empty);
                    OnFocusLost();
                }
            }
        }

        /// <inheritdoc/>
        [DataMember]
        public FocusBehavior FocusedMode { get; set; } = FocusBehavior.Set;

        /// <inheritdoc/>
        [DataMember]
        public bool IsExclusiveMouse { get; set; }
        /// <inheritdoc/>
        [DataMember]
        public bool UseKeyboard { get; set; }

        /// <inheritdoc/>
        [DataMember]
        public bool UseMouse { get; set; }

        /// <summary>
        /// If the object is a backing field for an interface implementation, this should be the object containing the
        /// field.  Otherwise, null.
        /// </summary>
        protected readonly IScreenObject OwningObject;

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <remarks>
        /// Takes one parameter which should be the Map class when this
        /// class is used as a backing object.  If this class is not being used as a backing object for an interface
        /// implementation, the parameter may be null.
        /// </remarks>
        /// <param name="owningObject">
        /// If the object is a backing field for an interface implementation, this should be the object containing the
        /// field.  Otherwise, null.
        /// </param>
        public MapScreenObject(IScreenObject owningObject = null)
        {
            OwningObject = owningObject ?? this;

            UseMouse = Settings.DefaultScreenObjectUseMouse;
            UseKeyboard = Settings.DefaultScreenObjectUseKeyboard;
            SadComponents = new ObservableCollection<IComponent>();
            ComponentsUpdate = new List<IComponent>();
            ComponentsRender = new List<IComponent>();
            ComponentsKeyboard = new List<IComponent>();
            ComponentsMouse = new List<IComponent>();
            ComponentsEmpty = new List<IComponent>();
            SadComponents.CollectionChanged += Components_CollectionChanged;
            Children = new ScreenObjectCollection(OwningObject);
        }

        /// <inheritdoc/>
        public virtual void Render(TimeSpan delta)
        {
            if (!IsVisible) return;

            foreach (IComponent component in ComponentsRender.ToArray())
                component.Render(OwningObject, delta);

            foreach (IScreenObject child in new List<IScreenObject>(Children))
                child.Render(delta);
        }

        /// <inheritdoc/>
        public virtual void Update(TimeSpan delta)
        {
            if (!IsEnabled) return;

            foreach (IComponent component in ComponentsUpdate.ToArray())
                component.Update(OwningObject, delta);

            foreach (IScreenObject child in new List<IScreenObject>(Children))
                child.Update(delta);
        }

        /// <inheritdoc/>
        public virtual bool ProcessKeyboard(Keyboard keyboard)
        {
            if (!UseKeyboard) return false;

            foreach (var component in ComponentsKeyboard.ToArray())
            {
                component.ProcessKeyboard(OwningObject, keyboard, out bool isHandled);

                if (isHandled)
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public virtual bool ProcessMouse(MouseScreenObjectState state)
        {
            if (!IsVisible)
                return false;

            foreach (IComponent component in ComponentsMouse.ToArray())
            {
                component.ProcessMouse(OwningObject, state, out bool isHandled);

                if (isHandled)
                    return true;
            }

            if (!UseMouse)
                return false;

            return false;
        }

        /// <inheritdoc/>
        public virtual void LostMouse(MouseScreenObjectState state) { }

        /// <inheritdoc/>
        public virtual void OnFocusLost() { }

        /// <inheritdoc/>
        public virtual void OnFocused() { }

        /// <inheritdoc/>
        public IEnumerable<TComponent> GetSadComponents<TComponent>()
            where TComponent : class, IComponent
        {
            foreach (IComponent component in SadComponents)
            {
                if (component is TComponent)
                    yield return (TComponent)component;
            }
        }

        /// <inheritdoc/>
        public TComponent GetSadComponent<TComponent>()
            where TComponent : class, IComponent
        {
            foreach (IComponent component in SadComponents)
            {
                if (component is TComponent)
                    return (TComponent)component;
            }

            return null;
        }

        /// <summary>
        /// Called when a component is added to the <see cref="SadComponents"/> collection.
        /// </summary>
        /// <param name="component">The component added.</param>
        protected virtual void SadComponentAdded(IComponent component) { }

        /// <summary>
        /// Called when a component is removed from the <see cref="SadComponents"/> collection.
        /// </summary>
        /// <param name="component">The component removed.</param>
        protected virtual void SadComponentRemoved(IComponent component) { }

        /// <inheritdoc/>
        public bool HasSadComponent<TComponent>(out TComponent component)
            where TComponent: class, IComponent
        {
            foreach (IComponent comp in SadComponents)
            {
                if (comp is TComponent)
                {
                    component = (TComponent)comp;
                    return true;
                }
            }

            component = null;
            return false;
        }


        private void Components_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (object item in e.NewItems)
                    {
                        FilterAddItem((IComponent)item);
                        ((IComponent)item).OnAdded(OwningObject);
                        SadComponentAdded((IComponent)item);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (object item in e.OldItems)
                    {
                        FilterRemoveItem((IComponent)item);
                        ((IComponent)item).OnRemoved(OwningObject);
                        SadComponentRemoved((IComponent)item);
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (object item in e.NewItems)
                    {
                        FilterAddItem((IComponent)item);
                        ((IComponent)item).OnAdded(OwningObject);
                        SadComponentAdded((IComponent)item);
                    }
                    foreach (object item in e.OldItems)
                    {
                        FilterRemoveItem((IComponent)item);
                        ((IComponent)item).OnRemoved(OwningObject);
                        SadComponentRemoved((IComponent)item);
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    List<IComponent> items = new List<IComponent>(ComponentsRender.Count + ComponentsUpdate.Count + ComponentsKeyboard.Count + ComponentsMouse.Count);

                    while (ComponentsRender.Count != 0)
                    {
                        ComponentsRender[0].OnRemoved(OwningObject);

                        if (!items.Contains(ComponentsRender[0]))
                            items.Add(ComponentsRender[0]);

                        FilterRemoveItem(ComponentsRender[0]);
                    }
                    while (ComponentsUpdate.Count != 0)
                    {
                        ComponentsUpdate[0].OnRemoved(OwningObject);

                        if (!items.Contains(ComponentsUpdate[0]))
                            items.Add(ComponentsUpdate[0]);

                        FilterRemoveItem(ComponentsUpdate[0]);
                    }
                    while (ComponentsKeyboard.Count != 0)
                    {
                        ComponentsKeyboard[0].OnRemoved(OwningObject);

                        if (!items.Contains(ComponentsKeyboard[0]))
                            items.Add(ComponentsKeyboard[0]);

                        FilterRemoveItem(ComponentsKeyboard[0]);
                    }
                    while (ComponentsMouse.Count != 0)
                    {
                        ComponentsMouse[0].OnRemoved(OwningObject);

                        if (!items.Contains(ComponentsMouse[0]))
                            items.Add(ComponentsMouse[0]);

                        FilterRemoveItem(ComponentsMouse[0]);
                    }
                    while (ComponentsEmpty.Count != 0)
                    {
                        ComponentsEmpty[0].OnRemoved(OwningObject);

                        if (!items.Contains(ComponentsEmpty[0]))
                            items.Add(ComponentsEmpty[0]);

                        FilterRemoveItem(ComponentsEmpty[0]);
                    }

                    foreach (IComponent item in items)
                        SadComponentRemoved(item);

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            void FilterAddItem(IComponent component)
            {
                if (component.IsRender)
                {
                    if (!ComponentsRender.Contains(component))
                        ComponentsRender.Add(component);
                }

                if (component.IsUpdate)
                {
                    if (!ComponentsUpdate.Contains(component))
                        ComponentsUpdate.Add(component);
                }

                if (component.IsKeyboard)
                {
                    if (!ComponentsKeyboard.Contains(component))
                        ComponentsKeyboard.Add(component);
                }

                if (component.IsMouse)
                {
                    if (!ComponentsMouse.Contains(component))
                        ComponentsMouse.Add(component);
                }

                if (!component.IsRender && !component.IsUpdate && !component.IsKeyboard && !component.IsMouse)
                {
                    if (!ComponentsEmpty.Contains(component))
                        ComponentsEmpty.Add(component);
                }

                ComponentsRender.Sort(CompareComponent);
                ComponentsUpdate.Sort(CompareComponent);
                ComponentsKeyboard.Sort(CompareComponent);
                ComponentsMouse.Sort(CompareComponent);
            }

            void FilterRemoveItem(IComponent component)
            {
                if (component.IsRender)
                {
                    if (ComponentsRender.Contains(component))
                        ComponentsRender.Remove(component);
                }

                if (component.IsUpdate)
                {
                    if (ComponentsUpdate.Contains(component))
                        ComponentsUpdate.Remove(component);
                }

                if (component.IsKeyboard)
                {
                    if (ComponentsKeyboard.Contains(component))
                        ComponentsKeyboard.Remove(component);
                }

                if (component.IsMouse)
                {
                    if (ComponentsMouse.Contains(component))
                        ComponentsMouse.Remove(component);
                }

                if (!component.IsRender && !component.IsUpdate && !component.IsKeyboard && !component.IsMouse)
                {
                    if (!ComponentsEmpty.Contains(component))
                        ComponentsEmpty.Remove(component);
                }

                ComponentsRender.Sort(CompareComponent);
                ComponentsUpdate.Sort(CompareComponent);
                ComponentsKeyboard.Sort(CompareComponent);
                ComponentsMouse.Sort(CompareComponent);
            }
        }

        /// <summary>
        /// Raises the <see cref="ParentChanged"/> event.
        /// </summary>
        /// <param name="oldParent">The previous parent.</param>
        /// <param name="newParent">The new parent.</param>
        protected virtual void OnParentChanged(IScreenObject oldParent, IScreenObject newParent)
        {
            UpdateAbsolutePosition();
            ParentChanged?.Invoke(OwningObject, new ValueChangedEventArgs<IScreenObject>(oldParent, newParent));
        }

        /// <summary>
        /// Raises the <see cref="PositionChanged"/> event.
        /// </summary>
        /// <param name="oldPosition">The previous position.</param>
        /// <param name="newPosition">The new position.</param>
        protected virtual void OnPositionChanged(Point oldPosition, Point newPosition)
        {
            UpdateAbsolutePosition();
            PositionChanged?.Invoke(OwningObject, new ValueChangedEventArgs<Point>(oldPosition, newPosition));
        }

        /// <summary>
        /// Called when the visibility of the object changes.
        /// </summary>
        protected virtual void OnVisibleChanged() =>
            VisibleChanged?.Invoke(OwningObject, EventArgs.Empty);

        /// <summary>
        /// Called when the paused status of the object changes.
        /// </summary>
        protected virtual void OnEnabledChanged() =>
            EnabledChanged?.Invoke(OwningObject, EventArgs.Empty);

        /// <inheritdoc/>
        public virtual void UpdateAbsolutePosition()
        {
            AbsolutePosition = Position + (Parent?.AbsolutePosition ?? new Point(0, 0));

            foreach (IScreenObject child in Children)
                child.UpdateAbsolutePosition();
        }

        /// <summary>
        /// Sorts the components based on the <see cref="IComponent.SortOrder"/> value.
        /// </summary>
        public void SortComponents()
        {
            ComponentsRender.Sort(CompareComponent);
            ComponentsUpdate.Sort(CompareComponent);
            ComponentsKeyboard.Sort(CompareComponent);
            ComponentsMouse.Sort(CompareComponent);
        }

        static int CompareComponent(IComponent left, IComponent right)
        {
            if (left.SortOrder > right.SortOrder)
                return 1;

            if (left.SortOrder < right.SortOrder)
                return -1;

            return 0;
        }

        /// <summary>
        /// Returns the value "ScreenObject".
        /// </summary>
        /// <returns>The string "ScreenObject".</returns>
        public override string ToString() =>
            "ScreenObject";

        /// <summary>
        /// Nothing.
        /// </summary>
        /// <param name="context">Nothing.</param>
        [OnSerializing]
        protected void OnSerializingMethod(StreamingContext context)
        {
            _childrenSerialized = Children.ToArray();
            _componentsSerialized = SadComponents.ToArray();
        }

        [OnSerialized]
        private void OnSerialized(StreamingContext context)
        {
            _childrenSerialized = null;
            _componentsSerialized = null;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            foreach (IScreenObject item in _childrenSerialized)
                Children.Add(item);

            foreach (IComponent item in _componentsSerialized)
                SadComponents.Add(item);

            _componentsSerialized = null;
            _childrenSerialized = null;

            UpdateAbsolutePosition();
        }
    }
}
