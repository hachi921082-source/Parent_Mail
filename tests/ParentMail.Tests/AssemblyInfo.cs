// SQLite の接続プールの解放（ClearAllPools）は全テストで共有される。
// 並行実行すると、別のテストが使っている最中の接続を巻き込んで、まれに失敗するため直列で実行する。
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
