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
dotnet publish .\VoxScope\VoxScope.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

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
