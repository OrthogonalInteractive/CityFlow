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

## 最初の実装順とテスト候補

下表はMVP全体のテスト計画。実装・実行済みの範囲と結果は末尾のStep別記録を参照。

| 順序 | 仕様 | 主に確認する振る舞い |
| --- | --- | --- |
| 1 | v0.1 §3–6 | 同色Sinkの即時消化、異色中継、Relay入力停止と再開、接続枠の境界 |
| 2 | §7 | 同色直結優先、直結満杯時の待機、削除予約Lineの除外、後続の別色が出発できること |
| 3 | §8–9 | FLOWの所属の一意性、受け取り完了まで容量を保持、追い越し防止、長さと容量の独立性 |
| 4 | §8.2・§11 | 削除予約と取消、詰まった終点での排出待ち、経路切替、接続枠の保持・解放、FLOW保存 |
| 5 | §9–11 | XZ経路長、Ground固定、制御点が有効でも区間が衝突するケース、Previewの非破壊性 |
| 6 | §5.2・§14.1・§16 | Sourceのみの敗北、猶予リセット、Pause中の更新停止と編集、Wave後の状態維持 |
| 7 | §12–15 | 入力→ユースケース→表示、カメラ切替時のPreview保持、ホバー、キャンセル、購読破棄 |

Source自身の超過生成、異色Buffer満杯のSinkへの同色到着、同色直結満杯時の待機、削除・切替の相互排他など、補完案に依存するテストには仕様節とその前提を英語コメントで残す。

## テストの配置

- `Tests/EditMode`：Domain / Applicationの振る舞い、Unityアセット・設定の検証。機能を追加したら対象モジュールごとのフォルダーへ分ける。
- `Tests/PlayMode`：シーン起動、DIの寿命、PlayerLoop、Input SystemとPresentationの連携。
- 見た目・操作感：EditorでOverview / Node 360 / 停止中の編集・ホバーを確認。自動テストの成功で描画品質を保証したことにしない。

基盤テストはURP割当、起動シーン登録、Input System設定、起動時のVContainer構築、UniTask / R3のPlayerLoop連携と購読破棄を確認する。
設定・接続・基本輸送のテストはStep 01〜03で追加済み。未実装の削除・切替・進行・入力等は、引き続き失敗するテストから追加する。

## 完了時の報告

実行したモード・件数・結果と、実行できなかった検証を明記する。
nullable警告や自作コードのコンパイルエラーを残さない。外部パッケージの警告は自作コードと区別し、全体の警告抑制で隠さない。

## Step 01 検証結果

- 設定・配置のRed: 14件中13件失敗（無検証のため不正値を受理）、1件成功。
- Green: EditMode 19/19、PlayMode 3/3成功。コンパイルError/Warningとも0。
- Ground外、建物内・クリアランス内、異なる高さ、非有限値、不正容量、Sink色不足、Sourceの生成先不足を検証。
- アセットから読み込んだ定義の独立性、BootstrapのVContainer、初期Node 5個・2色Sink・URP描画を確認。
- EditorのGame Viewスクリーンショット: `docs/screenshots/issue-1-city.png`。

## Step 02 検証結果

- 接続・集約のRed: 11件中10件失敗、1件成功。個別失敗理由と接続成功・生成未実装を確認。
- Green: EditMode 30/30、PlayMode 3/3成功。コンパイルError/Warningとも0、Console Error 0。
- 両端枠の同時確保、自己接続・重複・OUT/IN不足、逆方向、全区間障害物判定、読み取り専用スナップショット、FLOW IDとBuffer所属を検証。
- PlayModeで5本の固定配線、IN/OUT合計、生成されない初期状態を確認。
- EditorのGame Viewスクリーンショット: `docs/screenshots/issue-2-network.png`。


## Step 03 検証結果

- Routing・輸送のRed: 21件中20件失敗、1件成功。無処理の生成・移動・出発を検出。
- 混雑がない同時出発への不要な減速を追加テストで再現（1件失敗）し、停止列の間隔を混雑時だけ適用して修正。
- 最終Green: EditMode **52/52**、PlayMode **4/4**成功。コンパイルError/Warning **0**、Console Error **0**。
- 直結優先、複数同色直結、満杯直結待機、後続別色、空きLine間の乱数分岐、異色Sink中継、同色即時消化、固定容量、長距離の容量回復遅延を検証。
- 受け取り完了までのLine所属、FIFO、満杯異色Bufferでの同色受け取り、複数Incomingの容量競合、FLOW保存と一意性を検証。
- 0.05 s固定tickの生成→移動・受け渡し→出発を検証。30/60/144 fpsへ10秒を分割した場合と一括入力で、生成数・成功数・所属ID・移動距離が一致。
- PlayModeでは明示的tickによる生成・移動・消化、表示粒子の位置と消滅、設定アセット不変性を検証。元のEditor高速Play設定（Domain/Scene Reload無効）へ戻した後にも再起動を確認。
- スクリーンショット `docs/screenshots/issue-3-transport.png` はseed=1337、20秒時点で表示を固定して撮影。生成80、消化50、待機15、In-Flight15。長距離青Lineは10/10、短距離赤Lineは5/10。
- 対象はUnity Editor内。Playerビルドは今回の検証対象外。
