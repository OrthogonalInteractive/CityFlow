# TDDと検証

## 実行

初回に `dotnet tool restore` と `dotnet nugetforunity restore CityFlow` をリポジトリルートで実行する。Unity Editorの起動、コンパイル、テスト、ログ取得はすべてuloop CLIを使う。

```sh
uloop launch CityFlow
uloop --project-path CityFlow compile
uloop --project-path CityFlow run-tests --test-mode EditMode
uloop --project-path CityFlow run-tests --test-mode PlayMode
uloop --project-path CityFlow get-logs
```

`run-tests` の応答に含まれる実行件数・成功・失敗を確認し、テスト未実行を成功扱いしない。Unity Consoleにコンパイルエラーや例外がないことを `get-logs` で確認する。
Unity CLI Loopのツールが更新された場合は `uloop --project-path CityFlow sync` を実行する。利用可能なコマンドは `uloop --project-path CityFlow list` で確認する。

## Red → Green → Refactor

1. 仕様の節と、今回扱う振る舞いを選ぶ。「補完案」はその旨を記録する。
2. publicな振る舞いを検証する最小のテストを書く。
3. 実行して意図した失敗を確認する。依存DLL不足など環境上の失敗とは区別する。
4. 最小限の実装で通し、重複と責務を整理する。
5. 影響範囲のテストを再実行する。

DomainはUnityEngine参照を許可したままEditModeでテストする。独自Vector3やUnityの代替スタブを作らない。
時間・乱数・空間判定の入力を制御し、実時間待ちや偶然の分岐に依存しない。

## 検証対象

仕様§18ごとの実装・テスト対応は [完了条件の追跡](V01-Acceptance.md)、過去のRed/Greenと画面記録は [検証履歴](history/Testing-v01.md) を参照。

| 順序 | 仕様 | 主に確認する振る舞い |
| --- | --- | --- |
| 1 | v0.1 §3–6 | 同色Sinkの即時消化、異色Sinkへの転送禁止、Relay入力停止と再開、接続枠の境界 |
| 2 | §7 | 同色直結優先、直結満杯時の待機、削除予約Lineの除外、後続の別色が出発できること |
| 3 | §8–9 | FLOWの所属の一意性、受け取り完了まで容量を保持、追い越し防止、長さと容量の独立性 |
| 4 | §8.2・§11 | 削除予約と取消、詰まった終点での排出待ち、経路切替、接続枠の保持・解放、FLOW保存 |
| 5 | §9–11 | XZ経路長、Ground固定、制御点が有効でも区間が衝突するケース、Previewの非破壊性 |
| 6 | §5.2・§14.1・§16 | Sourceのみの敗北、猶予リセット、Pause中の更新停止と編集、Wave後の状態維持 |
| 7 | §12–15 | 入力→ユースケース→表示、カメラ切替時のPreview保持、ホバー、キャンセル、購読破棄 |

Source自身の超過生成、同色直結満杯時の待機、削除・切替の相互排他など、補完案に依存するテストには仕様節とその前提を英語コメントで残す。

## テストの配置

- `Tests/EditMode`：Domain / Applicationの振る舞い、Unityアセット・設定の検証。機能を追加したら対象モジュールごとのフォルダーへ分ける。
- `Tests/PlayMode`：シーン起動、DIの寿命、PlayerLoop、Input SystemとPresentationの連携。
- 見た目・操作感：EditorでOverview / Node 360 / 停止中の編集・ホバーを確認。自動テストの成功で描画品質を保証したことにしない。

基盤テストはURP割当、起動シーン登録、Input System設定、起動時のVContainer構築、UniTask / R3のPlayerLoop連携と購読破棄を確認する。
設定・接続・輸送・削除予約・経路編集・Wave・Pause・入力・ホバーの回帰テストを維持する。新規不具合は再現テストを先に追加する。

## 完了時の報告

実行したモード・件数・結果と、実行できなかった検証を明記する。
nullable警告や自作コードのコンパイルエラーを残さない。外部パッケージの警告は自作コードと区別し、全体の警告抑制で隠さない。

## 現在の検証状況

2026-09-22のUX改善は #14〜#17。全体検証はEditMode **130/130**、PlayMode **47/47**成功（Snapshot最適化まで）。その後のPauseヒント仕上げ・ホバー対象へのFフォーカス確認は、関連PlayMode **15/15**成功。最終コンパイルError/Warning **0**、撮影後Console Error/Warning **0**。

- Source 80%・猶予円弧・秒数・Pause・回復、混雑と予約の線種の併用、Sink始点禁止、空Line Undoを検証。
- Wave時計、カメラ移動後の通知回避、構造化ホバー、色の頭文字、自己候補除外、全マーカーの表示を検証。
- Snapshotの再利用、生成・接続・出発・輸送・予約・取消・Wave追加・Overload後の鮮度と、過去の読み取りの不変性を検証。変更のない1000回の読み取りでSnapshot実体は1000個→1個。FPSやGCバイト数の改善率は未測定。
- uloopによる実画面4枚を確認し、[UX対応報告](UX-Review-2026-09-22.md) に保存。制御したネットワークを撮影しており、人による難度比較の代用にはしない。
- 確認後はWiringLabを配線0・Pause・SimulationDriver有効へ戻した。Escで開始できる。

途中のRed/Greenは [検証履歴](history/Testing-v01.md#2026-09-22-ux改善進行中) に残している。

#12 / #13の人による比較プレイ、難度パラメータの最終採用は未完了。Unity Editor内を対象とし、Playerビルド・PLATEAU・高さ方向の探索は今回の検証対象外。
