# v0.1 完了条件の追跡

仕様§18とIssue #12の対応表。2026-09-22のUX改善前の基準はEditMode 122/122、PlayMode 39/39成功。以下は既存の自動検証の所在であり、人による比較プレイの完了を意味しない。改善Issueを閉じる際に結果・画面を紐付ける。

| §18の条件 | 自動検証 | 今回の追加確認 |
| --- | --- | --- |
| Ground上の接続 | FlowNetworkTests / StageConfigurationTests | #16 |
| 自動・手動の建物迂回 | GroundRoutingTests / ManualRouteTests | 既存検証 |
| Overview・Node 360・距離帯 | ConnectionSessionTests / NodeConnectionTests | #16 / #17 |
| Preview・XZ編集・確定 | ManualRouteTests / ManualRouteInputTests | #17 |
| 途中区間の貫通拒否 | GroundRoutingTests / ManualRouteTests | 既存検証 |
| 同色Sink優先・Relay限定分岐 | FlowTransportTests | 既存検証 |
| Line満杯待機・距離による輸送差 | FlowTransportTests / PlayPacingTests | #15 |
| IN/OUT上限 | FlowNetworkTests / ConnectionSessionTests | #16 |
| Pause中の配線・確認 | PauseTests / PauseInputTests | #14 / #16 / #17 |
| Node/Lineホバー | OverviewReadoutTests / HoverHudTests | #17 |
| Relay入力停止・排出後再開 | FlowTransportTests / BufferGaugeTests | 既存検証 |
| 停止FLOWの所属・容量保持 | CongestionTests / PlayPacingTests | #15 |
| Sourceのみ継続Overloadで敗北 | CongestionTests / SourceStatusTests | #14 |
| 排出完了後だけLine削除・枠解放 | LineLifecycleTests / LineLifecycleViewTests | #15 / #16 |
| 削除待ちの停止・取消 | LineLifecycleTests / LineLifecycleViewTests | #15 |
| 編集・削除でFLOW保存 | LineLifecycleTests / ManualRouteTests | 既存検証 |
| 同一シーンのWave追加・状態維持 | WaveTests / WaveSessionTests | #17 |

## 今回の改善

- #14: Sourceの敗北予告・Pause・回復。
- #15: 混雑と予約の線種、無彩色Relay、停止FLOWのサイズ。
- #16: Sink始点の禁止、空LineのUndo、自己候補除外、接続枠の用語。
- #17: Wave・通知・候補一覧・ホバー・操作案内の整理。
- #18: 入力アセット移行、Player対象化、既存未追跡ファイルの管理判断（保留）。

## #12 / #13に残す比較プレイ

- [ ] 完全Hub集中と分散構成を同一都市・固定条件で比較する。
- [ ] 長距離直結と短距離Relay経由の輸送量・配線操作時間を比較する。
- [ ] 危険予告だけでSourceを見つけ、Pause・再配線・回復へ到達できるか人が確認する。
- [ ] 生成間隔・速度・Wave倍率・敗北猶予の暫定値を最終採用する。

Player、PLATEAU、高さ、Port Unit、Widthは今回の完了判定に含めない。
