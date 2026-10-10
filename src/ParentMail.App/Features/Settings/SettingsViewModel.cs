using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ParentMail.App.Resources;
using ParentMail.Core;

namespace ParentMail.App.Features.Settings;

/// <summary>
/// 設定窓の「文字の大きさ」欄の表示モデル。数字の入力欄とスライダーは同じ値を指し、変えるとすぐ全画面に反映される。
/// 範囲外・数字でない入力は直前の値に戻し、エラー行を出す。
/// </summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ITextScale _scale;
    private string _textSizeText;
    private string _errorText = string.Empty;

    public SettingsViewModel(ITextScale scale)
    {
        _scale = scale;
        _textSizeText = Format(scale.TextSizePt);
        _scale.Changed += OnScaleChanged;
    }

    public string Title => Strings.ST_01;
    public string TextSizeLabel => Strings.ST_02;
    public string UnitLabel => Strings.ST_03;
    public string SampleText => Strings.ST_04;
    public string SliderName => Strings.ST_08;
    public string CloseLabel => Strings.ST_07;
    public string RangeHint => $"{Constants.MinTextSizePt:0} - {Constants.MaxTextSizePt:0}";
    public double Minimum => Constants.MinTextSizePt;
    public double Maximum => Constants.MaxTextSizePt;

    /// <summary>設定窓自身の文字サイズ（DIP）。文字サイズの変更に追従する。</summary>
    public double AppFontSize => _scale.FontSizeDip;

    /// <summary>見本の文字サイズ（DIP）。通常の画面と同じ大きさ。</summary>
    public double SampleFontSize => _scale.FontSizeDip;

    /// <summary>スライダーの値。範囲に丸めて文字サービスに渡す。</summary>
    public double TextSizePt
    {
        get => _scale.TextSizePt;
        set
        {
            _scale.TextSizePt = value; // 範囲への丸めと保存は文字サービスが行う
            SyncFromScale();
            ErrorText = string.Empty;
        }
    }

    /// <summary>数字の入力欄の文字。</summary>
    public string TextSizeText
    {
        get => _textSizeText;
        set
        {
            if (TryParse(value, out var pt))
            {
                ErrorText = string.Empty;
                _scale.TextSizePt = pt;
                SyncFromScale();
                return;
            }
            // 不正な値は直前の値に戻す。同じ文字列でも入力欄を戻すため通知を必ず出す。
            ErrorText = Strings.ST_06;
            _textSizeText = Format(_scale.TextSizePt);
            OnPropertyChanged(nameof(TextSizeText));
        }
    }

    /// <summary>入力欄の下の赤いエラー行。問題がなければ空。</summary>
    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value)) OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => _errorText.Length > 0;

    public void Dispose() => _scale.Changed -= OnScaleChanged;

    private void OnScaleChanged(object? sender, EventArgs e) => SyncFromScale();

    private void SyncFromScale()
    {
        _textSizeText = Format(_scale.TextSizePt);
        OnPropertyChanged(nameof(TextSizeText));
        OnPropertyChanged(nameof(TextSizePt));
        OnPropertyChanged(nameof(AppFontSize));
        OnPropertyChanged(nameof(SampleFontSize));
    }

    private static string Format(double pt) => pt.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool TryParse(string? input, out double pt)
    {
        pt = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        // 全角数字で入れても受け付ける
        var normalized = input.Trim().Normalize(NormalizationForm.FormKC);
        if (!double.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out pt)) return false;
        return pt >= Constants.MinTextSizePt && pt <= Constants.MaxTextSizePt;
    }
}
