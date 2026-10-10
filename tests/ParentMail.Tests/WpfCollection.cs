namespace ParentMail.Tests;

// WPF の Application はプロセスに 1 つしか作れず、作ったスレッドに結び付く。
// ウィンドウを実際に作るテストは、この集まりに入れて同時に走らせない。
[CollectionDefinition("Wpf", DisableParallelization = true)]
public sealed class WpfCollection
{
}
