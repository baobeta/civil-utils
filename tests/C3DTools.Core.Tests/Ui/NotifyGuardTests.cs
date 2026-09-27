using System.Collections.Generic;
using System.ComponentModel;
using C3DTools.Core.Presets;
using C3DTools.Core.Stations;
using C3DTools.Core.Ui;
using Xunit;

namespace C3DTools.Core.Tests.Ui;

public class NotifyGuardTests
{
    private sealed class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Raised;
        public void Raise(string name) => NotifyGuard.Raise(this, PropertyChanged, name);
    }

    [Fact]
    public void A_listener_that_raises_again_is_cut_at_the_maximum_depth_and_reported_once()
    {
        var reports = new List<string>();
        NotifyGuard.LoopDetected = reports.Add;
        try
        {
            var model = new Model();
            model.PropertyChanged += (s, e) =>
            {
                model.Raised++;
                model.Raise(e.PropertyName);   // what a binding that keeps writing a different value back does
            };

            model.Raise("Mode");

            Assert.Equal(NotifyGuard.MaxDepth, model.Raised);
            Assert.Equal(new[] { "Model.Mode" }, reports);

            model.Raise("Mode");   // the fuse resets: the next change is delivered and a new loop is reported again
            Assert.Equal(2 * NotifyGuard.MaxDepth, model.Raised);
            Assert.Equal(2, reports.Count);
        }
        finally
        {
            NotifyGuard.LoopDetected = null;
        }
    }

    [Fact]
    public void Ordinary_nested_changes_are_all_delivered()
    {
        var names = new List<string>();
        var session = new StakeGenerationSession(new ProjectPreset());
        session.PropertyChanged += (s, e) => names.Add(e.PropertyName);

        session.SetSource("T1", 0, 250, new[] { "G" });
        session.InsertMode = true;

        Assert.Contains(nameof(StakeGenerationSession.GenerateMode), names);
        Assert.Contains(nameof(StakeGenerationSession.CanApply), names);
    }

    [Fact]
    public void No_listener_is_fine() => new Model().Raise("X");
}
