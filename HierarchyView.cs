
using System.Numerics;
using System.Collections.Generic;
using Guid = System.Guid;

using Stride.Core;
using Stride.Engine;

using Hexa.NET.ImGui;
using Stride.Games;
using static Hexa.NET.ImGui.ImGui;
using static StrideCommunity.ImGuiDebug.ImGuiExtension;

namespace StrideCommunity.ImGuiDebug;
public class HierarchyView : ImGuiComponentBase
{
    private readonly Scene _rootScene;

    /// <summary>
    /// Based on hashcodes, it doesn't have to be exact, we just don't want to keep references from being collected
    /// </summary>
    HashSet<Guid> _recursingThrough = new HashSet<Guid>();
    List<IIdentifiable> _searchResult = new List<IIdentifiable>();
    string _searchTerm = "";

    const float DUMMY_WIDTH = 19;
    const float INDENTATION2 = DUMMY_WIDTH + 8;

    public HierarchyView(Scene rootScene)
    {
        _rootScene = rootScene;
    }

    public override void Draw(ImGuiSystem imGui, GameTime time)
    {
        if (InputText("Search", ref _searchTerm, 64))
        {
            _searchResult.Clear();
            if (string.IsNullOrWhiteSpace(_searchTerm) == false)
                RecursiveSearch(_searchResult, _searchTerm.ToLower(), _rootScene);
        }

        using (Child())
        {
            foreach (IIdentifiable identifiable in _searchResult)
            {
                RecursiveDrawing(identifiable);
            }

            if (_searchResult.Count > 0)
            {
                Spacing();
                Spacing();
            }

            foreach (var child in EnumerateChildren(_rootScene))
                RecursiveDrawing(child);
        }
    }

    void RecursiveSearch(List<IIdentifiable> result, string term, IIdentifiable source)
    {
        if (source == null)
            return;

        foreach (var child in EnumerateChildren(source))
        {
            RecursiveSearch(result, term, child);
        }

        string strLwr;
        if (source is Entity entity)
            strLwr = entity.Name.ToLower();
        else if (source is Scene scene)
            strLwr = scene.Name.ToLower();
        else
            return;

        if (term.Contains(strLwr) || strLwr.Contains(term))
            result.Add(source);
    }

    void RecursiveDrawing(IIdentifiable source)
    {
        if (source == null)
            return;

        string label;
        bool canRecurse;
        {
            if (source is Entity entity)
            {
                label = entity.Name;
                canRecurse = entity.Transform.Children.Count > 0;
            }
            else if (source is Scene scene)
            {
                label = scene.Name;
                canRecurse = scene.Children.Count > 0 || scene.Entities.Count > 0;
            }
            else return;
        }

        using (ID(source.Id.GetHashCode()))
        {
            bool recursingThrough = _recursingThrough.Contains(source.Id);
            bool recurse = canRecurse && recursingThrough;
            if (canRecurse)
            {
                if (ArrowButton("", recurse ? ImGuiDir.Down : ImGuiDir.Right))
                {
                    if (recurse)
                        _recursingThrough.Remove(source.Id);
                    else
                        _recursingThrough.Add(source.Id);
                }
            }
            else
                Dummy(new Vector2(DUMMY_WIDTH, 1));
            SameLine();

            if (Button(label))
                Inspector.GetOrCreateInspectorWindow(ImGuiScene).Target = source;

            using (UIndent(INDENTATION2))
            {
                if (recurse)
                {
                    foreach (var child in EnumerateChildren(source))
                    {
                        RecursiveDrawing(child);
                    }
                }
            }
        }
    }

    static IEnumerable<IIdentifiable> EnumerateChildren(IIdentifiable source)
    {
        if (source is Entity entity)
        {
            foreach (var child in entity.Transform.Children)
                yield return child.Entity;
        }
        else if (source is Scene scene)
        {
            foreach (var childEntity in scene.Entities)
                yield return childEntity;
            foreach (var childScene in scene.Children)
                yield return childScene;
        }
    }
}
