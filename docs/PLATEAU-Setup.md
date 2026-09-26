# PLATEAU SDKセットアップ

2026-09-26の指示により、v0.3に先立ってSDKの導入と互換性検証を行う。ゲームの実装対象は引き続きv0.1とv0.2 Δ1（高さ方向）。都市の選定、CityGMLの取り込み、障害物への変換、実在都市ステージは別作業とする。

同日の追加依頼による千代田区CityGMLの取り込みは、別の[東京駅周辺の都市データ確認シーン](PLATEAU-TokyoStation.md)に記録する。本書の検証結果はSDKセットアップ時点の記録。

## 環境と固定バージョン

| 項目 | 設定 |
| --- | --- |
| Unity | 6000.4.7f1 |
| レンダーパイプライン | URP 17.4.0 |
| 検証環境 | macOS / Apple Silicon（arm64） |
| PLATEAU SDK | 4.3.0 |
| 導入元 | `https://github.com/Project-PLATEAU/PLATEAU-SDK-for-Unity.git#v4.3.0` |
| 解決コミット | `a9830e9a86e8c5b28486ab0e318714ca4a20364c` |
| Git LFS | 3.6.1（この環境で確認） |

[v4.3.0の公式リリース](https://github.com/Project-PLATEAU/PLATEAU-SDK-for-Unity/releases/tag/v4.3.0)の動作確認環境は6000.3.10f1、推奨は同バージョン以上。6000.4.7f1での確認結果は下記に記録する。全SDK機能やPlayerビルドの動作保証とは区別する。

## 導入と復元

[公式のインストール手順](https://project-plateau.github.io/PLATEAU-SDK-for-Unity/manual/Installation.html)にあるGit URL方式を使用する。新しいUnityプロジェクトは作らず、既存の `CityFlow/` に導入する。

1. Unityを開く前に `git lfs version` が成功することを確認する。SDKのネイティブライブラリや画像はLFS管理のため、Gitだけでは足りない。
2. `CityFlow/Packages/manifest.json` の固定URLをUnity Package Managerが解決する。初回はネットワーク接続とインポート待ちが必要。
3. Unityが生成した `CityFlow/Packages/packages-lock.json` をmanifestと一緒に管理する。SDK本体や `Library/PackageCache` はコミットしない。
4. `uloop --project-path CityFlow compile`、関連テスト、`get-logs` で検証する。uloopコマンドは同じEditorに対して直列に実行する。

この環境ではSDKの依存としてAddressables 2.9.1、FBX Exporter 5.1.5、Autodesk FBX 5.1.3、Scriptable Build Pipeline 2.6.1、Timeline 1.8.12、Profiling Core 1.0.3が解決された。SDKのpackage.jsonに記載された要求バージョンと実際の解決バージョンは異なるため、再現時はlockを参照する。既存のURP、Shader Graph、Input System、R3、UniTask、VContainerの固定バージョンは維持する。

SDKの `AddressablesInitializer` はEditor初期化時にAddressablesの設定を自動生成する。SDK既定の `CityFlow/Assets/AddressableAssetsData/` を `.meta` とともに管理し、`EditorBuildSettings.asset` の設定参照も保存する。登録アセットは0件、Default Local Groupのみの初期状態。これはSDK生成設定であり、自作アセットの配置先は引き続き `Assets/CityFlow/` とする。PlayModeテストが変更したEditorの再生オプションは、検証後に元のDisableDomainReload / DisableSceneReloadへ戻した。

## 自動化できる範囲

| 作業 | 自動化と必要な入力 |
| --- | --- |
| SDK導入、バージョン固定、依存解決 | uloopからUnity Package Manager APIで実行できる |
| コンパイル、既存回帰テスト、Console確認 | uloopで実行できる |
| Macのネイティブライブラリ確認 | SDKの座標変換APIをuloopから呼んで確認できる |
| 都市データ取得・インポート | 対象都市、範囲、LOD、地物種別が決まればSDK APIで自動化できる。今回は未実施 |
| 座標系、マテリアル、Collider、シーン保存 | 対象データに合わせた設定を作成し、uloop経由で処理・検証できる。今回は未実施 |
| ゲームの障害物・Node配置への統合 | v0.3の実装作業。SDK導入だけでは完成しない |

取り込み前に必要なのは、都市・地区と範囲（目印やメッシュコードでも可）、データの年度、必要な地物・LODの選定。最初の検証用の提案値は「小範囲、建物LOD1、テクスチャなし」であり、確定仕様ではない。土地形状や背景の見た目が目的なら設定を変える。

[公式インポート手順](https://project-plateau.github.io/PLATEAU-SDK-for-Unity/manual/ImportCityModels.html)はローカルCityGMLとPLATEAUサーバーの両方に対応する。サーバーの既定URLは設定を省略できる。独自サーバーで認証が必要な場合は、その接続情報が別途必要。データ提供条件・出典は対象データごとに記録する。

SDKには `CityImporter.ImportAsync` があるため、範囲選択の画面操作を自動化の前提にしなくてよい。ゲーム側に導入する際は、SDKの型と座標変換をInfrastructure／Editorの境界に閉じ込め、Domain・ApplicationへSDK参照を追加しない。距離は1 Unity unit = 1 mを維持する。

## 検証結果

2026-09-26、Unity 6000.4.7f1 / macOS arm64で実施。

| 確認 | 結果 |
| --- | --- |
| SDK取得・UPM解決 | 4.3.0、上記コミットで登録済み |
| コンパイル | 成功、Error 0 / Warning 0 |
| CityFlow.Tests.EditMode | 171 / 171成功、Skipped 0 |
| CityFlow.Tests.PlayMode | 64 / 64成功、Skipped 0 |
| Mac用ネイティブライブラリ | `GeoReference.Create / Project / Unproject` の実行成功 |
| 最終Console | Error 0 / Warning 0 |
| 検証後のEditor | WiringLab、未保存変更なし、Play Mode停止 |
| Git差分 | 既存パッケージのバージョン変更なし、`git diff --check` 成功 |

実行コマンド：

```sh
uloop --project-path CityFlow compile
uloop --project-path CityFlow run-tests --test-mode EditMode --filter-type assembly --filter-value CityFlow.Tests.EditMode --unsaved-changes fail
uloop --project-path CityFlow run-tests --test-mode PlayMode --filter-type assembly --filter-value CityFlow.Tests.PlayMode --unsaved-changes fail
uloop --project-path CityFlow get-logs --log-type All --max-count 30
```

ネイティブAPIの確認はuloopの `execute-dynamic-code` から実行した。緯度35.681236、経度139.767125、高さ25 mを、平面直角座標系9・EUN・scale 1で投影し、`(-5992.91957042308, 25, -35363.2377449288)` を取得。逆変換後の緯度・経度誤差が0.0000001度以下、高さ誤差が0.00001 m以下で、UnityのYが高さ25 mとなることを確認した。これはライブラリの動作確認用座標であり、取り込む都市の選定ではない。

実都市データのダウンロード、CityGMLインポート、都市マテリアルの見た目、ゲームの障害物への統合、大規模都市の性能、Playerビルドは未検証。
