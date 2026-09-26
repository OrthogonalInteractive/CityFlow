# 東京駅周辺の都市データ確認シーン

2026-09-26の依頼に基づき、提供されたCityGMLから都市確認用の `TokyoStationInspection` を作成。同日の追加依頼で、WiringLab風の派生シーン `TokyoStationWiringLab` の駅前を配線ゲームとして操作できるようにした。Inspectionは元の都市確認用シーンを維持する。

## 開く場所と操作

- シーン：`CityFlow/Assets/CityFlow/Scenes/TokyoStationInspection.unity`
- 生成モデル：`CityFlow/Assets/CityFlow/Art/PLATEAU/TokyoStation/`
- **Sceneビュー**で閲覧する。右ボタンを押しながらW/A/S/Dで移動、Q/Eで上下、ホイールで速度調整。選択した建物をFでフレーム表示できる。
- Gameビューは保存された確認用カメラの固定表示。プレイヤー用の移動操作は追加していない。
- HierarchyはBuilding／Road／Relief／Vegetation／Bridgeに分かれる。種類ごとの表示を切り替えて地物を確認できる。
- 東京駅の基準点へSceneビューを戻す：`uloop --project-path CityFlow execute-dynamic-code --code 'CityFlow.Editor.PlateauTokyoStationSetup.FocusStation();'`

## TokyoStationWiringLabで遊ぶ

`CityFlow/Assets/CityFlow/Scenes/TokyoStationWiringLab.unity` を開いてPlayする。丸の内側の駅前にSource 1個、Relay 1個、Red／Blue／Yellow／Green／PurpleのSink 5個を配置し、配線0本から開始する。Hubは接続構成上の役割なので独立した種類としては配置しない。

- Source／RelayをクリックしてNode 360へ入り、候補をクリックして配線する。`S1 → R1` と `R1 → 各色Sink` で全色を配送できる。SourceからSinkへの直結も可能。
- **Pause／Resume** で時間を切り替える。Pause中も配線・編集・削除・カメラ操作ができる。
- Sourceは45秒の準備後、3秒間隔でFLOWを生成する。基本Buffer・Line容量・Source OverloadによるGame Over・Retryは既存ゲームと共通。一定Wave内で配線を試す小規模なプロトタイプとして、追加Waveは設定していない。
- 右ドラッグでOrbit、WASD／中ドラッグでPan、ホイールでZoom。Fで対象へフォーカス、Homeで駅前の初期俯瞰へ戻る。Escは配線・選択の取消専用。
- Shift＋Lineクリックで手動経路編集へ入り、Shift＋クリックで制御点を追加する。適用・削除は画面ボタンから行う。輸送中の変更・削除は排出を待つ既存ルールを使う。

### 範囲と暫定値

ゲームの範囲はX=-230〜-125 m、Z=-40〜85 mの105 × 125 m。7個のノードはその内部へまとめる。共通GroundはY=3.7 m、高さ配線は無効。ノードの大きさ・FLOW・Line・UIは既存ゲームと共通にしている。

都市の道路・地形メッシュをそのまま表示し、ゲームの配線は共通の水平面を使う。地面の起伏へ追従する完全な実在都市ステージではなく、駅前で配線と見た目を確認するための暫定構成。建物・橋梁の表示Boundsから範囲と交わる8個の直方体を障害物として保存する。全経路区間を同じ障害物で検証し、元メッシュより広く接続を制限する場合がある。

設定は `Settings/Gameplay/TokyoStationStage.asset` と `TokyoStationGameplay.asset`。Source OUTは6、Relay IN/OUTは3/5、Sink INは3。Source／RelayのBufferは10/5、Line容量は3、FLOW速度は12 m/s。準備時間・配置・カメラ・障害物近似・速度は、このシーン専用の調整値。

### 都市の見た目と組み立て

- 建物・橋梁は琥珀色の輪郭と窓グリッド、道路・地形は暗い青系のグリッド、植生は控えめな緑。元の都市FBXと輪郭メッシュを共用する。
- `AuthoredCityScenery` が都市のRendererを既存ゲーム表示へ渡し、簡易都市の地面や直方体を重ねて生成しない。Domain／ApplicationへPLATEAU型を追加しない。
- OverviewのNodeホバー／Fフォーカスで都市表示も減光する。Node 360では建物と輪郭を半透明にし、元のマテリアル配列・影設定へ復元する。Colliderは変更しない。
- 保存済みシーンにはゲーム設定を割り当て済み。起動シーンの先頭を維持したままBuild Settingsに追加し、Game Over後のRetryで再読み込みできる。

Sceneビューを駅前へ戻す：

```sh
uloop --project-path CityFlow execute-dynamic-code --code 'CityFlow.Editor.TokyoStationGameplaySetup.FocusStation();'
```

初回セットアップ用の `TokyoStationGameplaySetup.Create()` は、ゲーム未設定のTokyoStationWiringLabを開いてuloopから実行する。既存のゲーム設定やシーンを上書きしない。通常のPlay時には不要。

元のスタイル生成ツール `PlateauTokyoStationStyleSetup.Create()` は、派生シーン・`Art/PLATEAU/TokyoStationWiringLab`・`Art/Materials/TokyoStationWiringLab` が未作成の場合だけ使用する。都市形状・地物属性・Colliderは元のInspectionと共通で、見た目の詳細値は窓間隔3.2 × 4 m、輪郭の折れ角35度・最小長0.75 m。実際の窓配置を表すものではない。

### ゲーム化の検証（2026-09-26）

- Red：専用StageConfigurationが存在しないため、新規EditModeテストが想定どおり失敗。
- Green：コンパイルError／Warning 0、全EditMode **184/184**、全PlayMode **66/66**成功。
- 専用テストで7個・全種類・5色・未配線開始・全SinkへのRelay経由の到達可能性・範囲・障害物・Build Settings登録を確認。実シーンではPause中の配線、再開後の5色配送、都市マテリアル全スロットの透過／復元、Collider保持、Home復帰を確認した。
- uloopのInput Systemマウス入力でSource／Relayを選び、UI Toolkitのポインターイベントで候補・Pause／Resume・Retryを操作した。UI ToolkitはEventSystemを置かない構成のため、EventSystem向け`simulate-mouse-ui`は対象外。GameビューでNode 360と都市の透過表示を確認した。
- 画面操作で6本のLineを作り、Resume後の自然進行でDELIVERED 12を確認。保存アセットの初期Lineは0本のまま。
- 入力確認時、uloop 3.6.3の`MouseInputStateService.ApplyStateEvent`に起因するInput System Assertionを6件確認した。ゲーム実装の例外とは区別し、テスト本体は全件成功している。既存橋梁の大型三角形に関するPhysics警告も元データ由来の注意点として残る。
- 元のPlay Mode設定へ戻した通常起動でも7ノード・配線0本を確認し、この最終起動ではConsole Error／Warningともに0件。
- Inspectionシーンは元の保存データと一致。WiringLabの既存シーンブロックもカメラとSceneRoots以外は不変で、元の都市モデルやColliderを削除・再生成していない。

![駅前の配線と配送](screenshots/tokyo-station-gameplay.png)

![Node 360の配線候補と都市表示](screenshots/tokyo-station-node360.png)

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
