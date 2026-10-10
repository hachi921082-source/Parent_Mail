using System.Text.RegularExpressions;
using ParentMail.Core.Data;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.BlockList;

/// <summary>追加の結果。</summary>
public enum BlockAddResult { Added, Invalid, Duplicate }

// 受信拒否の入力確認と追加。ウィンドウと「この差出人を受信拒否」の両方がここを通る。
public sealed partial class BlockListEditor
{
    private readonly IBlockRepository _repo;

    public BlockListEditor(IBlockRepository repo) => _repo = repo;

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$")]
    private static partial Regex DomainShape();

    /// <summary>
    /// 入力を保存用の値（小文字、ドメインは @ なし）にする。形が正しくなければ false。
    /// ドメインは「example.com」でも「@example.com」でもよい。
    /// </summary>
    public static bool TryNormalize(BlockKind kind, string? text, out string value)
    {
        value = "";
        var t = TextNormalizer.NormalizeKey(text?.Trim());
        if (t.Length == 0) return false;
        if (kind == BlockKind.Address)
        {
            if (!ContactEmailValidator.IsValid(t)) return false;
            value = t;
            return true;
        }
        t = t.TrimStart('@');
        if (!DomainShape().IsMatch(t)) return false;
        value = t;
        return true;
    }

    public BlockAddResult Add(string accountId, BlockKind kind, string? text, out BlockEntry? entry)
    {
        entry = null;
        if (!TryNormalize(kind, text, out var value)) return BlockAddResult.Invalid;
        if (_repo.GetAll(accountId).Any(e => e.Kind == kind && TextNormalizer.NormalizeKey(e.Value) == value))
            return BlockAddResult.Duplicate;
        try
        {
            entry = _repo.Add(accountId, kind, value);
            return BlockAddResult.Added;
        }
        catch (DuplicateBlockEntryException)
        {
            return BlockAddResult.Duplicate;
        }
    }

    /// <summary>差出人のアドレスから「このアドレスだけ」または「このドメイン全体」を追加する。</summary>
    public BlockAddResult AddFromSender(string accountId, string address, BlockKind kind)
    {
        var text = address;
        if (kind == BlockKind.Domain)
        {
            var domain = BlockMatcher.DomainOf(TextNormalizer.NormalizeKey(address.Trim()));
            if (domain is null) return BlockAddResult.Invalid;
            text = domain;
        }
        return Add(accountId, kind, text, out _);
    }
}
