# VoxScope

`voice_changer_plan.txt` をもとにした Windows 向けボイスチェンジャー開発用の初期環境です。

## 開発環境

- .NET SDK 8.0.421
- WPF
- NAudio 2.3.0
- MathNet.Numerics 5.0.0

## よく使うコマンド

```powershell
dotnet restore
dotnet build .\VoxScope.sln
dotnet run --project .\VoxScope\VoxScope.csproj
```

## 配布ビルド

```powershell
dotnet publish .\VoxScope\VoxScope.csproj /p:PublishProfile=win-x64
```

出力先は `VoxScope\bin\Release\net8.0-windows\win-x64\publish\` です。

同じ内容の補助スクリプトも `scripts\publish-win-x64.ps1` に置いています。実行ポリシーで止まる環境では、上の `dotnet publish` コマンドを使うのが確実です。

## 設定保存

- 通常設定: `%APPDATA%\VoxScope\settings.json`
- プリセット: `%APPDATA%\VoxScope\presets.json`

## 仮想マイク

仮想マイク対応はまだ実装していません。設計メモは `docs\virtual-mic-integration.md` にまとめています。

## 初期構成

```text
VoxScope/
├─ Audio/
├─ Effects/
├─ Analysis/
├─ Presets/
├─ UI/
└─ Licenses/
```

最初の実装は、計画書どおり「マイク入力をそのまま出力する」Phase 1 から始めます。
