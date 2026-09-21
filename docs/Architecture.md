# アーキテクチャ

## 目的と現在の状態

Unity 6000.4.7f1上で、仕様書v0.1の配線UXと輸送ルールを検証する。
DDDを用いてルールの所有者と状態変更の境界を明確にし、EditMode中心のTDDで実装する。
この文書は後続機能の設計候補を含む。実装済みの範囲は末尾のStep 01〜03に記録する。

## モジュールとユビキタス言語

初期構成は単一プロセスのモジュラーモノリスとする。次の責務境界をDomain内部の名前空間とディレクトリに置き、必要になるまではモジュールごとのasmdefやサービスへ分割しない。

| モジュール | 所有する概念 | 責務と境界 |
| --- | --- | --- |
| FlowNetwork | FLOW、Node、Source、Sink、Relay、Buffer、Line、In-Flight | ネットワークの整合性、接続枠、局所FLOW Routing、容量、受け渡し、削除予約・取消、経路切替 |
| Spatial | LineRoute、経路長、Ground制約、通行可能領域 | 幾何経路の値と妥当性。建物の取り込み・Physics問い合わせ・探索アルゴリズムの実装は外部へ分ける |
| Progression | GameSession、Wave、生成予定、Overload猶予 | 同一都市内の進行、時間、生成する色の成立条件、SourceによるGame Over |

Hubは構成上の役割でありNode種別にしない。FLOW Routingは直接の接続先選択、Line Routingは障害物を避ける幾何経路生成であり、別のサービス・テストとして扱う。

## 集約と整合性

v0.1では、1都市の稼働中ネットワークを `FlowNetwork` 集約を採用する。NodeとLineを外部から個別に書き換えず、集約の操作を通す。これは接続作成・受け渡し・削除が複数のNodeとLineへ同時に影響するため。

- Line確定は始点OUT・終点IN・重複・経路を再検証し、両端の接続枠とLineを一緒に確定する。
- FLOWはNodeのBufferまたはLine上のいずれか1か所に属する。受け取り可否を確認し、所属の移動と容量更新を1つの操作として扱う。
- 削除予約や経路切替では、状態を変えて新規流入を止める。受け取り完了まではFLOWと接続枠を保持する。
- 外部へ渡す状態は読み取り専用ビュー・スナップショット・意味のある結果にする。コレクションを直接変更させない。
- GameSessionは進行を所有し、ApplicationがネットワークへのNode追加や生成を調整する。進行モジュールがネットワーク内部を直接変更しない。

集約のサイズ・更新コストは実測して見直す。初めからNodeごとにRepositoryを作ったり、分散トランザクション・イベントソーシングを導入したりしない。

## レイヤーとasmdef

```mermaid
flowchart TD
    Composition --> Presentation
    Composition --> Infrastructure
    Composition --> Application
    Composition --> Domain
    Presentation --> Application
    Presentation --> Domain
    Infrastructure --> Application
    Infrastructure --> Domain
    Application --> Domain
```

| アセンブリ | 責務 | 主な外部依存 |
| --- | --- | --- |
| CityFlow.Domain | 集約・値オブジェクト・ルール | UnityEngineの型を利用可。外部の事前コンパイルDLLは自動参照しない |
| CityFlow.Application | コマンド・問い合わせ・進行調整・ポート | UniTask。必要な通知にR3コア |
| CityFlow.Infrastructure | ポートの具象実装・設定変換 | Unity API、UniTask。将来SDKアダプター |
| CityFlow.Presentation | 入力・カメラ・表示・購読 | Input System、Cinemachine、URP、VFX Graph、UniTask、R3.Unity |
| CityFlow.Composition | VContainer登録・スコープ構築 | VContainer（`VContainer.Unity` 名前空間を含む） |
| CityFlow.Editor | 開発ツール | UnityEditor。Editor限定 |
| CityFlow.Tests.EditMode | ロジック・設定の検証 | Unity Test Framework。Editor限定 |
| CityFlow.Tests.PlayMode | 起動・Unityライフサイクルの検証 | Unity Test Framework。テストアセンブリとしてPlayer通常ビルドから除外 |

自作asmdefの `autoReferenced` はfalseとし、依存を明示する。事前コンパイルDLLも `overrideReferences` / `precompiledReferences` で明示する。Domainは `noEngineReferences: false` でUnityEngineを利用できる。
R3コアはNuGetのDLLなので、UPMアセンブリ `R3.Unity` とは参照方式が異なる。DomainにはR3を持ち込まず、通知への変換をApplication / Presentationで行う。

## Unityとの境界

Domainを別の.NET専用プロジェクトへ移植すること自体を目標にしない。`Vector3`、`Bounds`、`Mathf` などはそのまま使用する。
一方、状態の正本をMonoBehaviour、Transform、VFX粒子数に置かない。Unityのシーンがなくても値を与えてルールを検証できる設計にする。

- MonoBehaviourはUnityライフサイクルと入力・描画のアダプター。
- ScriptableObjectは調整用の設定。起動時に検証してドメインへ渡し、実行中のBuffer等を設定アセットに保存しない。
- VFX GraphはFLOWの表示。輸送・停止・消化・容量はDomainの状態から描画へ反映する。
- VContainerはCompositionで組み立てる。Domain / ApplicationのクラスはDI属性を要求しない。
- UniTaskは読み込み・非同期探索などの境界に使用する。R3はHUDや選択状態などの通知に使用する。
- 購読・CancellationToken・スコープは所有者と寿命を一致させる。

## 時間、Pause、再現性

Applicationのシミュレーション更新入口に明示的な時間差分を渡し、Domainで `Time.deltaTime` やグローバル乱数を直接参照しない。ランダム選択は差し替え可能な乱数源または選択結果を入力にする。

Pauseはシミュレーション更新を止める。生成・移動・受け渡し・Wave・Node追加・Overloadタイマーは同時に停止する。一方、入力・カメラ・ホバー・Previewは表示側の時間で動かす。`Time.timeScale = 0` だけをPauseの契約にしない。

FLOWが残るLineの削除・経路切替は再開後に進める。既に空のLineの削除など、時間を必要としないコマンドはPause中も実行できる。

tick内の「生成・移動・受け渡し・出発・Overload評価」の詳細な順序は最初のシミュレーション実装時にテストで固定する。描画フレームレートによる二重転送・容量超過を許さない。

## 経路とPLATEAUへの拡張

Application側に必要になった時点で次のポートを追加する。先に空のインターフェース群を量産しない。

| 境界の候補 | v0.1 | v0.2 / v0.3 |
| --- | --- | --- |
| ステージ形状の供給 | 簡易都市のFootprint・範囲 | 建物ボリューム、PLATEAU座標変換・形状の簡略化 |
| 経路生成 | XZ上の1候補 | 3D・最大2〜3候補 |
| 経路検証 | Ground固定・全線分の衝突判定 | 上昇下降も含めた全区間の検証 |

経路生成と検証は同じステージ形状・クリアランス・単位を使う。Previewも確定も同じ検証を通す。描画とFLOW移動、長さ計測が参照する確定済み経路を一致させる。

PLATEAU SDKの型や座標系はInfrastructureのアダプターに閉じ込め、ゲーム側へUnityローカル座標と障害物形状を渡す。**1 Unity unit = 1 m** を基準にする。簡易都市も同じ境界を使って比較検証を残す。

Port Unit、Width、方向反転はv0.2で仕様に沿って導入する。v0.1の接続本数を架空のPortモデルに置き換えて先取りしない。

## 保留事項

- Node追加方式とWave構成（手動設計／自動生成／組み合わせ）。
- Ground経路探索アルゴリズム、ステージ規模、固定tick幅、性能目標。
- UIの具体構成、入力バインド、描画の最適化方式。
- セーブ／ロード、対象プラットフォーム、PLATEAUの対象都市とSDKバージョン。

仕様を根拠に小さなテストで必要性を示し、決定時にこの文書へ反映する。

## Step 01：検証都市（実装済み）

BootstrapのComposition Rootは `GameplaySettings` と `StageConfiguration` を検証し、コピーした読み取り専用 `StageDefinition` をVContainerへ登録する。Presentationはその定義からGround、建物、Nodeを描画する。実行時状態をScriptableObjectへ書き戻さない。

検証都市は120 × 90 m、Ground Y=0、10 mグリッド、6棟、Source 1・Relay 2・赤／青Sink各1。中央建物の両側と6 m幅の通路で後続の配線検証を行う。NodeのGround・範囲・建物Footprint（クリアランス拡張）を起動時に検証する。全区間の経路検証はLine導入時に同じ定義を使う。

§17の暫定値としてMaxBuffer=50、MaxInFlight=10、Speed=20 m/s、Overload猶予=5 sを保持する。検証用の追加暫定値はクリアランス0.5 m、各Node IN/OUT=3、Source生成間隔0.25 s（長距離Lineの満杯を再現する負荷）。Overloadの敗北判定は後続Issueで実装する。

`City Flow > Set Up Validation City` は不足する設定アセットを作り、Bootstrapへ割り当てるEditor用の明示的セットアップ。通常の起動では不要。既存の調整値を上書きしない。

## Step 02：FlowNetwork集約（実装済み）

`FlowNetwork` がNodeのBuffer・Incoming/OutgoingとLineのIn-Flightを一括所有する。Node IDはステージ内で一意の文字列、Line IDとFLOW IDは集約内で発行する連番。外部入力でFLOW IDや所属を差し替える操作は公開しない。Hubは種別に追加しない。

`TryConnect` は端点存在、自分自身、同方向重複、OUT/IN上限、経路の順で検証する。成功したときだけLineと両端の接続枠を同時に確保し、失敗時は `ConnectionFailure` を返して状態を維持する。逆方向接続は独立したLine。初期配線もこの操作を通す。

経路は `LineRoute` へコピーし、Ground高さ、有限値、ゼロ長区間、端点、通行範囲、全線分とクリアランス付きFootprintの交差を検証する。XZ実長、描画座標、後続の移動処理は同じ折れ線を参照する。経路探索・配線操作は後続Issue。

公開する `NetworkSnapshot` は生成時点のコピーで、Node/Line/Buffer/In-Flightのコレクションは読み取り専用。UnityオブジェクトやTransformを状態の正本にしない。Sourceへの `GenerateFlow` は同色Sinkの存在を検証し、§5.2補完案に沿って超過分も保持する。Step 02のシーンは生成更新を呼ばず静止する。

検証アセットは5 Node・5 Line。Sourceから赤Sinkへの42 m直線と青Sinkへの169 m迂回を含む。HUDに各NodeのIN/OUT使用数、Lineの方向を表示する。

## Step 03：基本輸送と更新順（実装済み）

`FlowSimulation.Tick(deltaSeconds)` が入力時間を蓄積し、暫定固定幅 **0.05 s** ごとに次を実行する。0秒では何も進めず、負値・NaN・Infinityは拒否する。描画フレーム時間を切り捨てず、遅いフレームでも必要なtickを順に処理する。

1. **生成**：Sourceごとの予定時刻に従い、現在のSink色を重複排除して等確率で選ぶ。最初の生成は生成間隔経過後。Source自身の超過生成も保持する（§5.2補完案）。
2. **移動・受け渡し**：既存In-Flightを共通速度で進め、Line作成順・各Lineの出発順に受け取りを試す。同色Sinkは即時消化し、異色はBufferへ渡す。受け取り容量不足ならLine上に保持する。容量は受け取り完了後だけ解放する。
3. **出発**：Nodeはステージ定義順、Bufferは待機順に評価。同色Sink直結があれば空き直結から選び、全部満杯なら待機。他の色を後続から評価する。直結がない場合は空きOutgoing間から等確率で選ぶ（§7補完案）。このtickで出発したFLOWは次tickから移動する。

受け渡されたFLOWは同じtickの出発段階で中継できるが、次のLineの移動を同じtickに重ねない。複数Incomingが競合する場合はLine作成順を暫定採用し、受け取るたびにBuffer容量を再評価する。公平性制御は今回の対象外。

混雑時の追い越しを禁止し、終端側から待機する（§8.1補完案）。停止列の見える間隔は暫定0.8 m、短いLineでは `Length / MaxInFlight` 以下に制限する。運行中に混雑がなければ全FLOWが共通速度で移動する。移動位置は `LineRoute.PositionAt` による実経路上の位置。表示だけ0.9 m上へオフセットする。

乱数境界 `IRandomSource` をテストで差し替え、実シーンは設定の固定seed（暫定1337）で `System.Random.Next` を使う。Presentationの `SimulationDriver` はUnityのフレーム時間をApplicationへ渡すだけで、輸送ルールを持たない。表示粒子はIn-Flightのスナップショットから作成・更新・削除し、ゲーム状態の正本にはしない。

初期ネットワークは短い赤直結42 m（2.1 s）と長い青直結169 m（8.45 s）に同じ容量10を持つ。Sourceは0.25 s間隔で生成するため、青側が先に満杯になりSourceのBufferが増える。HUDに処理成功数・Buffer・In-Flight・Line実長・所要時間・容量使用数を表示する。Overload敗北、Wave、プレイヤー配線、削除・経路切替は後続Issueの対象。
