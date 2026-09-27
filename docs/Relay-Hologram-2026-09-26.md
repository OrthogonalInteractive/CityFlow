# Relayの高さホログラム（2026-09-26）

Relayの配置Yから接続可能な上限まで、淡い青白色のホログラムを表示する。上限はDomainの`StageDefinition.ConnectionCeiling`をそのまま使い、`MaximumRise`とステージ天井の両方を反映する。上昇能力0のRelayには柱を表示しない。

円柱の天面が上限の実座標に一致し、1 m間隔の帯で高さを読み取れる。上部へ向けて透明度を上げ、天面の内側は輪郭より薄くする。直径4.8 m、線幅0.035 m、上端の不透明度倍率0.45は暫定の見た目設定。色・帯の間隔・透過率は`RelayHologram.mat`で調整できる。

基部の球・パッド・Bufferゲージを維持する。柱はColliderと影を持たず、配線の衝突判定を変えない。フォーカス時は対応するNodeと一緒に減光し、Node 360では始点の柱を隠す。Waveで追加されたRelayにも同じ表示を適用する。常設ラベルは追加しない。

同日の追加指示により、柱の側面・天面もホバー／クリックで選択できる。Overviewで柱をクリックするとそのRelayから配線を開始し、Node 360では接続先の柱からも確定できる。クリック高度で経路高度は変えない。Node本体を優先し、柱同士が重なれば手前を選ぶ。柱内の既存LineはShift＋クリックで編集できる。選択には有限円柱とカメラRayの交差を使い、Colliderは追加しない。

## 実画面

HeightLabの6／10／22／26 mのRelay。uloopから実行中だけ検証用の配線を作り、設定済みWaveを220秒まで進めた。12 Node・13 Line、61配送。シーンとStageアセットにデモ配線は保存していない。

![Relayの高さを表すホログラム](screenshots/v02-relay-hologram-overview.png)

1036×757のNode 360。始点R4の柱は非表示になり、R5の26 mの柱と配線を確認できる。候補への配線は既設のため、画面にはその理由が表示されている。

![Node 360でのホログラム](screenshots/v02-relay-hologram-node360.png)

## 検証

- Red：高さ表示が存在しない旧実装で、新規PlayModeテスト2件の失敗を確認。
- Green：新規2件成功。初期／追加Relay、高所配置、ステージ天井による切り詰め、能力0、表示範囲と実座標、Collider不在、基部の選択、減光、Node 360の非表示／復帰を確認。
- 既存の`ValidationCityTests`・`HeightInteractionTests`・`ConnectionFocusTests`は9/9成功。Ground専用シーンに柱が出ないことも確認。
- コンパイルError／Warning **0**。専用シェーダーの診断メッセージ **0**。画面確認後のConsole Error／Warning **0**。
- 1600×900のOverviewと1036×757のNode 360をuloopで撮影し、透明度・帯・配線の見え方を確認。

確認後はPlay Modeを終了し、HeightLabを初期状態へ戻した。Playerビルドは今回の検証対象外。

## 柱からの選択の追加検証

- Red：柱のホバー選択とマウスクリックでNode 360へ入れないことを、新規PlayMode 2件で再現。
- Green：`RelayHeightVisualTests` 4件を含む、Overview・Node 360・高さ編集・接続フォーカスの関連PlayMode **26/26成功**。側面・天面、高所の下端／ステージ天井、能力0、近クリップ面が柱内にある場合、カメラ背面の除外、接続先の柱クリック、重なる垂直LineのShift＋クリックを確認。
- 回帰テストの初回はUnityから`RunFinished`通知が返らず600秒でタイムアウト。Console Error／Warningは0で、未保存変更がないことを確認してuloopでEditorを再起動し、同じ26件を再実行して成功した。
- `TokyoStationWiringLab`で、実行時だけWave追加予定のNodeを配置し、R3の柱（基部から100 m上）のクリックでNode 360へ入ることを確認。屋上S2から、基部が画面外のR2の柱（Y=175 m）をホバーして有効なPreviewを表示し、クリックで`S2 → R2`を作成できた。検証用の配置・Lineはアセットに保存していない。
- コンパイルError／Warning **0**、画面確認後のConsole Error／Warning **0**。確認後は東京駅シーンのPlay Modeを終了し、GameビューのサイズとPlay設定を検証前の値へ戻した。

![屋上Sourceから、地上の本体が画面外にあるRelayの柱を選択](screenshots/tokyo-relay-column-selection.png)
