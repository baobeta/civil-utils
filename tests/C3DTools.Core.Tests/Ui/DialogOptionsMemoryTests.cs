using System;
using System.IO;
using C3DTools.Core.Ui;
using Xunit;

namespace C3DTools.Core.Tests.Ui;

public class DialogOptionsMemoryTests
{
    [Fact]
    public void Returns_the_fallback_for_a_missing_option()
    {
        var memory = new DialogOptionsMemory();

        Assert.Equal(1100.0, memory.Get("CTYTC", "Width", 1100.0));
        Assert.True(memory.Get("CTYTC", "Khung", true));
        Assert.Equal("x", memory.Get("CTYTC", "Name", "x"));
    }

    [Fact]
    public void Round_trips_bool_double_int_and_string_through_json()
    {
        var memory = new DialogOptionsMemory();
        memory.Set("CTYTC", "Khung", false);
        memory.Set("CTYTC", "Width", 1234.5);
        memory.Set("CTTOADO", "Decimals", 3);
        memory.Set("CTFONT", "Font", "Times New Roman");

        var back = DialogOptionsMemory.Load(memory.Save());

        Assert.False(back.Get("CTYTC", "Khung", true));
        Assert.Equal(1234.5, back.Get("CTYTC", "Width", 0.0));
        Assert.Equal(3, back.Get("CTTOADO", "Decimals", 0));
        Assert.Equal("Times New Roman", back.Get("CTFONT", "Font", ""));
        Assert.Equal(4, back.Count);
    }

    [Fact]
    public void Keys_are_command_dot_option()
    {
        var memory = new DialogOptionsMemory();
        memory.Set("CTYTC", "Height", 600.0);

        Assert.Contains("\"CTYTC.Height\": \"600\"", memory.Save());
        Assert.Equal("CTYTC.Height", DialogOptionsMemory.Key("CTYTC", "Height"));
    }

    [Fact]
    public void Stores_doubles_with_a_dot_under_vietnamese_culture()
    {
        TestCulture.Run("vi-VN", () =>
        {
            var memory = new DialogOptionsMemory();
            memory.Set("CTYTC", "Width", 1100.25);

            Assert.Contains("1100.25", memory.Save());
            Assert.Equal(1100.25, DialogOptionsMemory.Load(memory.Save()).Get("CTYTC", "Width", 0.0));
        });
    }

    [Fact]
    public void Keys_ignore_case()
    {
        var memory = DialogOptionsMemory.Load("{ \"CTYTC.Width\": \"900\" }");

        Assert.Equal(900.0, memory.Get("ctytc", "width", 0.0));
    }

    [Fact]
    public void Returns_the_fallback_for_a_value_of_the_wrong_type()
    {
        var memory = DialogOptionsMemory.Load("{ \"CTYTC.Width\": \"rộng\", \"CTYTC.Khung\": \"1\" }");

        Assert.Equal(1100.0, memory.Get("CTYTC", "Width", 1100.0));
        Assert.Equal(7, memory.Get("CTYTC", "Width", 7));
        Assert.True(memory.Get("CTYTC", "Khung", true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    public void Invalid_json_loads_as_empty(string json)
    {
        Assert.Equal(0, DialogOptionsMemory.Load(json).Count);
    }

    [Fact]
    public void Rejects_unsupported_types()
    {
        var memory = new DialogOptionsMemory();

        Assert.Throws<NotSupportedException>(() => memory.Set("CTYTC", "When", DateTime.Now));
        Assert.Throws<NotSupportedException>(() => memory.Get("CTYTC", "When", 1.5f));
    }

    [Fact]
    public void Saves_and_loads_a_file_creating_its_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "c3dtools-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "C3DTools", "options.json");
        try
        {
            var memory = new DialogOptionsMemory();
            memory.Set("CTYTC", "Width", 800.0);
            memory.SaveFile(path);

            Assert.Equal(800.0, DialogOptionsMemory.LoadFile(path).Get("CTYTC", "Width", 0.0));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void A_missing_file_loads_as_empty()
    {
        var path = Path.Combine(Path.GetTempPath(), "c3dtools-missing-" + Guid.NewGuid().ToString("N"), "options.json");

        Assert.Equal(0, DialogOptionsMemory.LoadFile(path).Count);
    }
}
