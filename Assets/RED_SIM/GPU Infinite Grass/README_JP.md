GPU Infinite Grass v.1.1.0
こちらからアセットを入手できます：
Patreon: https://www.patreon.com/posts/150937086/
Booth: https://redsim.booth.pm/items/7997362

テクニカルサポート: https://discord.gg/TCRAUBs

# GPU Grass - Usage Guide (JP)

このガイドでは、アセットの動作、各機能が何を制御するか、そして調整時に注意すべき細かなポイントを説明します。

## Quick Setup (Basic)

1. `Grass Particle Surface Manager` プレハブをシーンにドラッグします。
2. 地面サーフェス用のメッシュオブジェクトを `Surfaces` リストに割り当てます。
3. Manager インスペクターの `Paint` ツールでメッシュに草をペイントします。ペイント先メッシュには十分な頂点数が必要です。
4. `Draw Amount` と `Draw Distance` を調整して、草密度と描画距離を制御します。
5. 自分の草テクスチャを設定し、`Particle Surface Manager` 下部の Grass Material で最終的な見た目と挙動を調整します。

## Manager Settings

### Surface Settings

- `Surface Camera`: Surface Mask テクスチャを描画するシステムカメラです。このカメラは指定して Render Texture のターゲットを設定しておけば基本的に触る必要はありません。向きや配置位置はほぼ不問で、高さマップとカラーマスクの描画用です。（ただしワールド中心から極端に離しすぎないでください）
- `Camera Layer`: Surface Camera が使うカリングレイヤーです。空のレイヤーにする必要があります。そうしないと草が誤描画され、余計な描画負荷も増えます。
- `Mask Material`: サーフェスメッシュ情報を Surface Mask Render Texture にどうエンコードするかを定義します。通常は付属の `Surface Mask` マテリアルを使ってください。独自の草描画ロジックを作る場合のみ変更を検討します。
- `Surfaces`: 草を描画する対象 `MeshFilter` のリストです。このアセットは複数の垂直レベルへ同時描画はできません。必要ならレベルごとに複数の `Particle Surface Manager` を使ってください。
- `Draw Distance`: プレイヤー周囲で草を描画する半径を制御します。
- `Target Override`: 草の描画中心をプレイヤーカメラから固定/移動 Transform に切り替えます。比較的小さなワールドならワールド中心を指定して静的に配置でき、可視領域がプレイヤー追従しなくなります。
- `Always Update Surface`: 毎フレームでマスクを再描画します。サーフェス形状やマスクデータが動的に変わる場合のみ有効です。無効時はプレイヤー移動時のみ再描画され、パフォーマンスを節約できます。
- `Surface RT Resolution`: Surface Mask Render Texture のサイズです。`Custom` を選ぶと Render Texture 側で手動設定できます。アスペクト比は必ず 1:1 にしてください。崩れると草が誤描画されます。モバイルでは小さい解像度を使って負荷を下げてください。

### Trail Settings

- `Enable Trail`: Trail 機能を無効化して負荷を節約できます。Grass Shader 側の Trail 処理も同時に無効になります。
- `Trail Material`: Trail 情報を書き込みます。通常は付属プレハブの `Trail` マテリアルのままで問題ありません。カスタムに変更する必要はほぼありません。
- `Trail CRT`: 草の曲げベクトルを時間経過で減衰させながら保持する永続 Custom Render Texture です。
- `Trail Decay`: 既存 Trail データのフェード速度です。
- `Trail Targets`: 草に影響を与える追加の移動 Transform です。Scale が曲げ半径に影響します。
- `Trail CRT Resolution`: Trail 用 Custom Render Texture のサイズです。`Custom` で手動設定できます。アスペクト比は必ず 1:1 にしてください。崩れると草が誤描画されます。モバイルでは小さい解像度を使って負荷を下げてください。

### Particle Settings

- `Particle Material`: メインの Grass Material です。
- `Rendering Layer`: 草を描画するレイヤーです。可視レイヤーであれば任意に使えます。
- `Cast Shadows` / `Receive Shadows`: リアルタイムシャドウ機能です。
- `Draw Amount`: 描画する草バッチ数です。1 バッチあたり 16383 草クアッドなので、最小描画数は 16383 パーティクルになります。

## Surface Painting

ペイントはサーフェスメッシュの頂点カラーを編集します。

- `R`, `G`, `B` チャンネルは Shader の `Grass Type R/G/B` に対応します。
- ペイント強度はタイプの出現量とサイズ挙動に影響します。
- 同じ領域で複数チャンネルを有効にすると、草タイプを混在させられます。
- 必要な草チャンネルだけを Grass Shader で有効化し、不要なものは無効化して負荷を下げてください。

Tools:
- `Brush`: ソフトフォールオフ付きのローカルペイント。
- `Eraser`: 選択チャンネルをソフトフォールオフ付きで削除。
- `Fill`: ヒット地点から接続されたメッシュ領域を塗りつぶし。

Important behavior:
- 初回ペイント時、ツールはメッシュコピー（`*_Painted`）を作成するため、元メッシュは変更されません。
- ペイントの細かさは頂点密度に制限されます。滑らかなペイントには対象領域に十分な頂点数が必要です。

## Grass Material

### Grass Type R / G / B

各タイプは独立したスタイルブロックです。
- タイプごとに有効/無効化して負荷を削減。
- テクスチャソースモード（`Single Texture`, `Array Random`, `Array By Size`）を選択。`Array By Size` はブレードサイズ進行に応じてテクスチャを選び、`Array Random` はバリエーション重視です。
- カラーグラデーション、ブレード形状、ランダム性、ローカル風反応などを調整。

### Common

- `Visible Amount`: 全体密度の可視量コントロール。
- `Cutoff`: 草テクスチャのアルファクリップ閾値。
- `Bottom Blending`: 草と地面の境界をなじませます。
- `Mask Threshold`: 草を生成するために必要な最小マスク値。
- `Size Threshold`: スケーリング後に小さすぎるブレードを除去。
- `YBias`: 草全体の垂直オフセット。
- `Enable Triple Cross`: 各パーティクルを 3 ブレードクラスタとして描画します。

### Grass Simplifying

- `Fade`: エリア境界に向かって草を縮小します。
- `Culling`: エリア境界に向かってランダム間引きを増やします。
- `Simplifying`: 遠距離簡略化を早める/遅らせる設定です。
- `Simplifying Fade`: 簡略化遷移の滑らかさを制御します。

### Trail

- `Trail Brightness`: 影響を受けた草の色暗化量。
- `Trail Bend`: Trail データ由来の曲げ強度。

### Clouds / Subsurface Scattering / Wind

- `Clouds`: 草表面にアニメーションするライティング変調を加えます。常に必要とは限りません。雲影を使うなら地形シェーダー側にも実装し、草シェーダーと同期させる必要があります。
- `Subsurface Scattering`: 光源方向を向いたときに逆光透過感を追加します。VRC Light Volumes のライティングも考慮されます。
- `Wind`: レイヤー化されたワールド空間風モーションです。

### Advanced

- `ShadowPass Depth Write`: Shadow Caster パスでの深度書き込みを制御します。
- `Render Queue`: シーンのソート影響を理解している場合のみ変更してください。

## Optimization Tips

負荷を下げるときは次の順で調整してください。

1. まず `DrawAmount` を調整。値を上げると頂点数とオーバードローが同時に増えます。
2. simplification/fade/culling を使って品質とパフォーマンスのバランスを取る。
3. 全体の草密度を低めに保つ（`DrawAmount` / `RenderDistance` の比率）。
4. Grass Material で未使用機能をすべて無効化する。
5. シーンでリアルタイムシャドウを使っていて草の影が不要なら、高コスト設定（`CastShadows` / `ReceiveShadows`）を無効化する。
6. すべての Render Texture は小さめを維持する。特にモバイル/Quest では重要です。（Quest では 512x512 が推奨上限目安。より小さいほど軽量です）

- インスペクターのパフォーマンス指標はガイドであり、最終的なプロファイル結果ではありません。必ずシーンで手動確認してください。
- 草描画が本当に必要なメッシュだけを割り当ててください。
- 見た目とパフォーマンスは必ず実ターゲット環境で検証してください。

## Troubleshooting

草が表示されない、または挙動が不安定な場合は以下を確認してください。

- `Surfaces` が空ではない。
- Material 側で Grass Types（`Enable Type R/G/B`）が有効になっている。
- `SurfaceCamera`, `MaskMaterial`, `ParticleMaterial` および RT 参照が有効。
- `MaskThreshold` / `SizeThreshold` が実質的に全消し設定になっていない。
- 正しい描画レイヤーを設定している。
- VRChat へアップロード後に草がペイントされない: `Project Settings -> Player -> Optimize Mesh Data` をオフにする。
- 草が縞模様またはノイズっぽいパターンになる: これはデフォルト Example シーンの Surface Mask マテリアルです。`Particle Surface Manager` の現在の Surface Mask マテリアルを選択し、そこから grass pattern texture を外してください。

## Extra Tips

このアセットは、Editor の Scene View をモニターのリフレッシュレートで描画するように強制します。これは意図的な仕様で、無効時にシーン内の草が多いと残るバックグラウンドアプリ由来のパフォーマンス問題を回避するためです。
手動で元に戻す場合: `Preferences -> General -> Interaction Mode -> Default`

このアセットは Project Settings の `Optimize Mesh Data` も自動で無効化します。Unity のこの機能は、すべてのメッシュから未使用の頂点属性を削除します。GPU Grass は Vertex Color 属性を使用しており、この機能が有効だと削除される場合があります。
手動で再有効化する場合: `Project Settings -> Player -> Optimize Mesh Data`

## How the Asset Works

- Mesh generation and painting:
  - Editor スクリプトが指定サーフェスメッシュまたは Terrain のコピーを生成し、元データを変更しません。
  - ユーザーは草マスクをペイントし、サーフェスメッシュの RGB 頂点カラーとして保存されます。
- Surface mask pass:
  - 専用の正射影カメラが、割り当てたサーフェスメッシュを `MaskMaterial` で Render Texture に描画します。
  - Mask の RGB は頂点カラー由来で、草タイプ分布を定義します。
  - Mask の A はワールド高さ情報を保持し、草の垂直配置に使われます。
- Optional trail interaction pass:
  - `TrailCRT` が動的な曲げベクトルを保持します。
  - `TrailMaterial` がプレイヤー/ターゲットからの干渉情報をそのテクスチャへ書き込みます。
  - Grass Shader がそれを読み取り、触れた草を曲げて暗くします。
- Grass particle render pass:
  - 草は `ParticleMaterial` を使う GPU インスタンスパーティクルとして描画されます。
  - 草はアクティブな描画中心（プレイヤーカメラまたは `TargetOverride`）の周囲に生成されます。
  - 草の生成順は六角スパイラル形状に従い、正しいソートでオーバードローを抑えます。
