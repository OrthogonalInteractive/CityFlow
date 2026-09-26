# 東京駅周辺の都市データ確認シーン

2026-09-26の依頼に基づく、提供されたCityGMLを確認するための独立したシーン。ゲームのSource・Relay・Sink、FLOW、Line Routing、Waveは組み込まない。既存のBootstrap／WiringLab／HeightLabと起動シーン設定を維持する。

## 開く場所と操作

- シーン：`CityFlow/Assets/CityFlow/Scenes/TokyoStationInspection.unity`
- 生成モデル：`CityFlow/Assets/CityFlow/Art/PLATEAU/TokyoStation/`
- **Sceneビュー**で閲覧する。右ボタンを押しながらW/A/S/Dで移動、Q/Eで上下、ホイールで速度調整。選択した建物をFでフレーム表示できる。
- Gameビューは保存された確認用カメラの固定表示。プレイヤー用の移動操作は追加していない。
- HierarchyはBuilding／Road／Relief／Vegetation／Bridgeに分かれる。種類ごとの表示を切り替えて地物を確認できる。
- 東京駅の基準点へSceneビューを戻す：`uloop --project-path CityFlow execute-dynamic-code --code 'CityFlow.Editor.PlateauTokyoStationSetup.FocusStation();'`

## データと範囲

| 項目 | 内容 |
| --- | --- |
| 提供ZIP | `13101_chiyoda-ku_pref_2025_citygml_1_op.zip` |
| データ | 東京都千代田区、2025年度の3D都市モデル |
| データ作成日 | 2026-03-13（同梱READMEの記載） |
| ZIP SHA-256 | `d903d0e59a93475c8684284ac52e9b2b5cba972208dec258641fc8942ca42ee2` |
| 取り込みメッシュ | `53394611`、`53394621` |
| 選定範囲 | 東京駅を含む街区と、その北側の大手町方面 |
| 位置の基準 | 緯度35.681236、経度139.767125、高さ0 m |
| 平面直角座標系 | 9系、EUN（X=東、Y=上、Z=北） |
| 距離 | 1 Unity unit = 1 m |
| 建物 | LOD1〜2、利用可能な最高LODを表示、テクスチャあり |
| 道路・植生 | LOD1〜3、利用可能な最高LODを表示 |
| 地形・橋梁 | 収録LODのうち1〜2、選定範囲内 |
| マテリアル | SDKのURP対応マテリアル、テクスチャ結合4096 px |

原本の建物GMLには2メッシュで2,345棟のBuilding要素があり、`53394611`に名称「東京駅」の要素を4件確認した。この原本件数は取り込み後のRenderer数とは異なる。範囲外の地物、重複LOD、BuildingPart等により件数は変わる。

元ZIPは約2.1GB、全展開は約7.3GB。必要な地物、テクスチャ、codelists、schemas、metadata、READMEのみを `.local-data/plateau/chiyoda-2025-tokyo-station/` へ展開した（8,039ファイル、約1.9GB）。地形の入力は大きい2次メッシュのGMLを使い、SDKで指定範囲へ切り出す。地理院の航空写真タイル取得は無効にし、提供されたデータで確認する。

## 再生成

初回の空の出力先を対象に、リポジトリルートから実行する。

```sh
python3 scripts/prepare_plateau_tokyo_station.py /path/to/13101_chiyoda-ku_pref_2025_citygml_1_op.zip
uloop --project-path CityFlow compile
uloop --project-path CityFlow execute-dynamic-code --code 'CityFlow.Editor.PlateauTokyoStationSetup.Start(System.IO.Path.GetFullPath("../.local-data/plateau/chiyoda-2025-tokyo-station")); return CityFlow.Editor.PlateauTokyoStationSetup.Status;'
```

取り込みはEditor上で非同期に進む。地物を種類ごとに取り込み、SDKのAssets保存機能でFBX・テクスチャを保存してからシーンを保存する。元データの展開先と検証用スクリーンショットはコミット対象外。保存済みシーンと参照アセット・`.meta`を通常のGitで管理し、都市データにはGit LFSを使わない。作成済みシーンの閲覧にはZIPと展開先を必要としない。

```sh
uloop --project-path CityFlow execute-dynamic-code --code 'return CityFlow.Editor.PlateauTokyoStationSetup.Status;'
```

`Completed`で完了。`Cancel()`でキャンセルできる。未保存シーンがある場合、Play Mode中、既に出力先がある場合はStartを拒否し、既存の作業を上書きしない。途中失敗時はConsoleを確認する。再生成のために既存成果物を削除する場合もuloopのAssetDatabase操作を使う。

SDK依存はEditorアセンブリだけに追加し、Domain・Application・ゲームの空間判定への依存は追加していない。シーン内のSDK属性コンポーネントは都市データの確認に利用する。

## 出典

出典：東京都／Project PLATEAU「3D都市モデル 千代田区（2025年度）」、ユーザー提供ZIP。同梱READMEで提示されたライセンスのうちCC BY 4.0を選択する。Unity向けに範囲選択、座標変換、LOD表示の選択、テクスチャ結合、FBX変換を行った。元データのREADMEとmetadataは展開先に保持する。データは現在の建築状況を保証するものではない。

## 確認結果

Unity 6000.4.7f1 / macOS arm64 / PLATEAU SDK 4.3.0で確認。

| 確認 | 結果 |
| --- | --- |
| コンパイル | Error 0 / Warning 0 |
| ProjectConfigurationTests（EditMode） | 4 / 4成功：URP、Input System、起動シーン設定 |
| BootstrapTests（PlayMode） | 2 / 2成功：既存シーンのDI構築、UniTask・R3連携 |
| 保存後のシーン再読み込み | 成功、5種類の都市モデルを復元 |
| メッシュ | 3,866個、合計1,455,448頂点、欠落0 |
| マテリアル | 172個、うち165個にテクスチャ、非対応シェーダー0 |
| 東京駅 | 原本で確認した4件の建物IDが有効なオブジェクトとして存在 |
| 座標範囲 | Bounds中心 `(147.92, 117.72, 193.90)`、半径方向の各成分 `(682.50, 123.82, 1056.83)` m |
| 表示 | Sceneビューの俯瞰・駅舎拡大、Gameビューの固定カメラで確認 |
| 生成アセット | 約2.4GB（モデル・テクスチャ等177ファイル、全ファイルの.metaを確認） |
| Git差分 | `git diff --check`成功。既存シーン、Build Settings、Editor再生設定の変更なし |

ConsoleのErrorは0。元データの橋梁メッシュ `brid_74d13577-7e6f-4f5f-8481-cab3020e6fdd` に500 unitを超える辺の三角形があり、UnityのMeshColliderが物理判定の安定性に関するWarningを1件出す。形状・テクスチャは表示できているが、この橋梁Colliderをゲームの衝突判定に採用する検証は行っていない。警告抑制や原本形状の変更はしていない。

地形の航空写真は取得しておらず、地形の見た目はSDKの既定マテリアルによる。起動時のスムーズさ、大規模都市のフレームレート、Playerビルド、ゲームへの組み込みは未検証。全ゲームテストの再実行ではなく、上記6件の関連テストと新規シーンの実データ検証を行った。
