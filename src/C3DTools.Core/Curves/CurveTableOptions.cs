using System.ComponentModel;
using System.Globalization;

namespace C3DTools.Core.Curves;

/// <summary>State of the CTYTCBANG dialog: which outputs to write for the picked route.</summary>
public sealed class CurveTableOptions : INotifyPropertyChanged
{
    private bool _writeTable = true, _writeCsv = true, _writeXlsx;
    private int? _curveCount;

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>"Bảng": AutoCAD Table at a picked point.</summary>
    public bool WriteTable { get => _writeTable; set => SetOutput(ref _writeTable, value, nameof(WriteTable)); }

    /// <summary>"CSV": &lt;drawing&gt;_YEUTOCONG.csv.</summary>
    public bool WriteCsv { get => _writeCsv; set => SetOutput(ref _writeCsv, value, nameof(WriteCsv)); }

    /// <summary>"Excel": &lt;drawing&gt;_YEUTOCONG.xlsx.</summary>
    public bool WriteXlsx { get => _writeXlsx; set => SetOutput(ref _writeXlsx, value, nameof(WriteXlsx)); }

    /// <summary>Curves of the loaded route; null while no route is loaded.</summary>
    public int? CurveCount
    {
        get => _curveCount;
        set
        {
            if (_curveCount == value) return;
            _curveCount = value;
            Raise(nameof(CurveCount));
            Raise(nameof(CanApply));
            Raise(nameof(SummaryText));
        }
    }

    public bool CanApply => _curveCount > 0 && (_writeTable || _writeCsv || _writeXlsx);

    public string SummaryText =>
        _curveCount == null ? "Chưa chọn tuyến"
        : _curveCount == 0 ? "Tuyến không có đường cong nào"
        : _curveCount.Value.ToString(CultureInfo.InvariantCulture) + " đường cong";

    private void SetOutput(ref bool field, bool value, string name)
    {
        if (field == value) return;
        field = value;
        Raise(name);
        Raise(nameof(CanApply));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
