# 影子工坊迁移 / Shadow Workshop

本模块最初迁自影子原型的工作副本，在 MetaHackathon2026 总项目的 `/main/TimBranch/shadow-workshop` 分支开发。本仓库是从总项目拆出的独立工程，只含影子关卡和 Shape of Memory；下文提到的工坊、车辆等总项目内容不在本仓库中。

![迁移后的三愿入口](ShadowWorkshopPreview.png)

## 试玩入口

- 打开 `Assets/ShadowWorkshop/Scenes/EverydayObjects.unity` 进入 **A LIFE**，见下文日常物件关卡；用 **< 上一关** 可回到帆船、月球和三愿。
- **Three Wishes / 三愿**：用 12 枚拼块依次满足灯塔、左前 45° 松树、右前 45° 火箭；每一步要求所有已开放方向同时合格。松手稳定一秒后按 **REVEAL**。
- **MOON LEVEL** 进入九块探索机器人，**BOAT LEVEL** 进入三块帆船教学。月球关的 **THREE WISHES** 返回三愿。
- 每关前排新增 **< 上一关 / 下一关 >**，按 三愿 → 月球 → 帆船 → A LIFE → ITALIA → NIPPON → ENCORE → BRIGHT IDEA → GOOD BOY → BIRTHDAY → BON VOYAGE → GOOD MORNING → HOME 循环。A LIFE 起三关是日常物件关卡，ENCORE 起三关是手电筒关卡，BIRTHDAY 起四关是手电筒第二轮（固定灯、摆弄物堆），见下文。场景可设置 `ShadowDemo.Playlist`，只在指定关卡间循环（`TorchLevels2.unity` 只含第二轮四关）。
- 持有拼块时禁止切关。切换影子关卡会开始该关的新一轮；帆船的显式本地存档仍可通过 **SAVED HULL** 恢复。
- 也可直接打开 `Assets/ShadowWorkshop/Scenes/ThreeWishes.unity`、`MoonCourier.unity` 或 `ShadowWorkshop.unity`。每个入口用共享玩家 prefab `Assets/MetaHackthone/Generated/MetaHackathonPlayer.prefab` 生成自己的玩家。
- Unity 菜单 **MetaHackthone > Shadow Workshop > Play Three Wishes**；重建三个入口用 **Create or rebuild scenes**。此操作保留已有 build scenes 顺序并添加影子场景，不替换项目入口。

## 已复用的总项目能力

| 能力 | 接入方式 |
| --- | --- |
| 玩家、头追、手模型、透视和 Stage 地面 | `MetaPlayerRig` 与现有共享玩家 prefab；不新增 XR loader，不移动 tracking origin |
| Meta 手关节 | `MetaShadowPlayer` 从同一 rig 的 `Oculus.Interaction.Input.Hand` 读取世界坐标、手腕旋转和指尖；系统手势或无效追踪不驱动物件 |
| 射线捏合和近触按钮 | `MetaMenuButton` 的通用回调入口、`MetaPinchSelector.BindToRay`。拼块持有期间禁用菜单操作 |
| 场景生命周期 | 每个影子场景由 `ShadowDemo` 用 `StandalonePlayer` 生成玩家；影子关卡在同一会话内替换内容，保留同一个玩家 |
| 吸附计算 | `SnapGeometry.AroundAxis`（原工坊 45° 面连接的旋转候选搜索）；影子网格使用 5 mm / 15° 参数 |

影子拼块是悬停摆放的投影谜题，仍使用自身的抓放/换手/撤销事务；没有给它们套用车辆的刚体连接、socket 占用、重力或 45° 限制。帆船保持连续摆放；月球和三愿的 **GRID** 可关闭，网格相对工作台，不吸附标准答案。追踪丢失保留当前位置，不执行网格吸附或回托盘。

## 操作

Quest：靠近拼块捏合，移动并转动手掌。保留原型 18 mm 抓取、20 mm 即时释放、张开约 80 ms 才能再次抓取的规则，避免一次捏合跳到邻近拼块。另一只手可以接管，整次操作只有一笔撤销。按钮使用项目原有射线捏合或近触。**LOWER / RAISE / RECENTER** 只移动谜题工作台。

桌面：左拖平移、右拖旋转、滚轮深度、Q/E 滚转；1–9 选择前九块（全部拼块均可鼠标选取）；Z 撤销、H 提示，月球/三愿 L 开关网格。点击实体按钮进行揭晓、恢复、重置和返回。切关会清空该影子关的临时进度；不会覆盖工坊存档。

## 日常物件关卡 / Everyday objects

三关都是单屏、单一正面平行光，6 件日常物件，没有分阶段，所有物件自由摆放，无需相互接触。

| 关卡 | 物件 → 影子 |
| --- | --- |
| **A LIFE** | 皮球（头）、课本（弯着的上身）、滑板（腿）、笔记本电脑侧面（弯曲的手臂）、鼠标（鞋）、雨伞（手杖）→ 拄拐的老人 |
| **ITALIA** | 帕玛森奶酪轮（北部）、比萨斜塔纪念品（半岛，斜 45°）、橄榄油瓶（脚跟）、辣椒（脚尖）、比萨饼（西西里）、番茄罐头（撒丁岛）→ 意大利地图 |
| **NIPPON** | 饭团（北海道）、小芥子人偶（东北）、达摩（关东/中部）、新干线（关西/中国）、乌冬碗（四国）、招财猫（九州）→ 日本地图 |

- 目标形状 = 物件在答案姿态下的影子并集，由运行时用导入的网格光栅化，所以一定可解；换成美术网格后目标自动跟着变。地图海岸线只是屏幕上的装饰线，不参与评分。
- **放宽的胜利条件**：影子边缘 3 像素（约 13 mm）内都算重合；目标覆盖 ≥ 90%，每件物件自己的答案区域被覆盖 ≥ 80%，总溢出 ≤ 7%，单件物件的溢出 ≤ 其答案区域的 12%（用来拦住转错方向的物件），并且 6 件都必须离开托盘。松手静止 0.8 秒自动完成，没有 REVEAL 按钮。实测：每件偏一格 5 mm、或任意一件偏 15°，都能通过；徒手 6 mm / 6° 随机误差约 96–100% 通过；塔、滑板、电脑、雨伞、新干线、小芥子等转 90° 不通过。参数在每个 `SilhouetteLevel` 资源上可调。
- **模糊影子**：屏幕把实时影子做 3 次 3×3 盒式模糊，边缘约 1–1.5 像素的半影；超出容差的部分显示琥珀色，尚缺影子的目标区域显示斜纹。**MATCH %** 按每件物件区域的平均覆盖增长。**HINT** 12 秒内显示提示文字，并让手里（或悬停）物件的答案区域在屏幕上闪烁。
- 桌面：同上，另加方向键 / `[` `]` 把选中物件按 15° 精确转动；从托盘拖起的物件会放到底座中间深度。
- 直接进入：**MetaHackthone > Shadow Workshop > Play Everyday Objects**（`Scenes/EverydayObjects.unity`，初始关卡 A LIFE，已加入 build scenes，也是工坊 **Shadow Workshop** 按钮的入口；**Create or rebuild scenes** 和 **Create everyday-objects scene** 都会重建它并登记到 build 列表）。

管线：

1. `Tools/ShadowArt/build_everyday.py`（numpy + Pillow）用基础体（车削、挤出、管道、球、盒）生成粗模、材质颜色、答案和托盘姿态，写入 `Assets/ShadowWorkshop/Art/Everyday/EverydayObjects.json`，预览写到 `Logs/ShadowArt/Everyday-*.png`，并报告抖动下的通过率。答案必须在 5 mm / 15° 网格上（脚本会断言）。`--fit` 是地图关卡的摆位辅助：在各自岛屿内网格搜索，结果需人工确认后写回脚本。
2. **MetaHackthone > Shadow Workshop > Import everyday objects** 生成/原地更新 `Art/Everyday/Meshes/*.asset`、`Art/Everyday/Materials/*.mat`（URP Lit，沿用灰盒材质的 shader）和 `Resources/ShadowArt/Levels/<id>.asset`。GUID 保持不变。
3. 新增关卡：在脚本 `LEVELS` 加一项并导入，然后把 id 加到 `ShadowDemo.Levels` / `LevelNames`。
4. 运行时：`SilhouetteLevel`（数据）、`SilhouetteProjection`（光栅化与放宽评分）、`SilhouetteWorkshop` / `SilhouetteWorld` / `SilhouetteInput`（玩法、显示、输入，结构与月球关一致）。替换网格时必须保持 Read/Write 可读。

## 手电筒关卡 / Torch levels

一堆日常物件用细钉固定在转盘上，组成一个固定的"杂物堆"。手电筒是点光源，影子投在后墙上。只有某个转盘角度加某个手电位置能让影子变成和这些物件有关的图案。

| 关卡 | 物件 → 影子 |
| --- | --- |
| **ENCORE** | 黑胶唱片、CD（两个符头）、麦克风、鼓棒（符干）、口琴（符杠），磁带和拨片藏在影子里 → ♫ 双八分音符 |
| **BRIGHT IDEA** | 台钟（灯泡）、笔筒（灯颈）、便利贴和橡皮（螺口）、铅笔、圆珠笔、荧光笔（光芒）→ 灯泡 |
| **GOOD BOY** | 狗粮袋（身体）、网球（头）、洁齿棒（嘴）、袜子（耳朵）、骨头和牛皮棒（腿）、绳结玩具（尾巴）、狗碗 → 站着的狗和它的碗 |

- **Quest**：手电绑定右手，光束沿 Meta 的 pointer pose（与系统射线同一条），镜头在手掌前 9 cm；手部追踪丢失时手电留在原处。左手在转盘附近（离轴 30 cm 内）捏合并绕轴移动，转盘跟手转，像转餐桌转盘。**LIGHT IN RIGHT / LEFT**（SWAP HANDS）交换左右手。转盘被握住时禁止切关、返回和完成判定。
- **桌面**：左键拖动转转盘；右键拖动让手电绕物堆转动（方位/高度），始终对准物堆；滚轮调距离；A/D 转盘，W/S 升降手电，Q/E 左右绕，H 提示。
- **判定（位置和大小无关）**：只取手电光锥内看得见的影子，按质心和 RMS 半径归一化到 64×64，再与答案影子比较：1 格容差，覆盖 ≥ 90%，多余 ≤ 10%，IoU ≥ 0.62。保持 0.5 秒即完成。2 格容差时出现过"缺了符杠也能过"（IoU 0.58）的情况，所以加了 IoU 下限。实测窗口：ENCORE 转盘 ±9°、手电高度 ±12°，另两关约 ±15–20°；手电距离 0.7–1.4 倍都可以；三关各 60 个随机视角全部不通过。墙上方的 **WARMTH** 以每关自己的"错误角度平均值"为零点。**HINT** 显示提示文字和右侧的目标图案卡片；完成后卡片一直显示。
- **显示**：墙是 160×120（9 mm/像素）的 CPU 纹理，30 Hz 重画，只有转盘、手电位置或方向变化时才重画；编辑器内单次约 1.3–1.8 ms。评分 10 Hz。影子做两次 3×3 模糊作为手电的半影；完成后变清晰，光色转为关卡主题色。另有一个不投影的 URP 聚光灯照亮物件。3 mm 细钉小于一个墙像素，不计入影子。
- 物件中心不能进入光源 16 cm 内（同时防止所有三角形铺满整面墙）。
- 直接进入：**MetaHackthone > Shadow Workshop > Play Torch Levels**（`Scenes/TorchLevels.unity`，初始关卡 ENCORE；不加入 build scenes，关卡在任何影子场景中都能通过上一关/下一关进入）。重建用 **Create torch-levels scene**，**Create or rebuild scenes** 也会一并重建它。

管线：

1. `Tools/ShadowArt/build_torch.py`（复用 `build_everyday.py` 的基础体）在"墙面画面"里设计：每个物件给出影子在墙上的位置（相对物堆中心的投影）和放大率，脚本把它放到从答案光源出发的那条射线上，于是物件深度各不相同，从别的角度看就是一堆杂物。脚本会打印转盘/高度/距离通过窗口和随机视角误判数，并检查物件是否在转盘范围内；预览写到 `Logs/ShadowArt/Torch-*.png`（答案影子、俯视物堆、归一化目标、其他三个角度）。
2. **MetaHackthone > Shadow Workshop > Import torch levels** 生成 `Art/Torch/Meshes`、`Art/Torch/Materials` 和 `Resources/ShadowArt/Torch/<id>.asset`（`TorchLevel`），原地更新。
3. 运行时：`TorchLevel`、`TorchProjection`（光栅化、归一化、评分）、`TorchWorkshop` / `TorchWorld` / `TorchInput`。`ShadowPlayerContext.ReadAim` 是新增的可选接口，`MetaShadowPlayer` 用 `IHand.GetPointerPose` 实现。
4. 未在真机验证：pointer pose 作为手电方向的手感、手抖对 0.5 秒保持的影响、Quest 上的 CPU 开销。

## 手电筒关卡第二轮 / Torch levels 2

反馈：手持手电离物堆太近，光锥和透视都过大。第二轮把灯固定，所有自由度交给物堆。物堆装在一个“球”里（三道大圆环 + 中心小球）：捏住球壳拖动就像滚球一样自由转动（任意轴），捏住中心小球就能搬动。真机反馈过用手腕转动来映射偏航/俯仰不顺手（手腕的横滚被丢弃，想转的方向转不动），所以改为滚球：转动来自手臂移动而不是手腕，范围大，也不受手腕追踪噪声影响。

- **房间**：桌后是一个墙角，左墙和右墙各成 45°。每面墙由对角的一盏灯照亮（右前灯照左墙，左前灯照右墙），灯装在支架上、距物堆静止位约 0.8 m、光锥半角 20°，墙面放大约 1.6 倍（第一轮约 2.2 倍）。物堆的姿态 = 位置 + 任意旋转（四元数）；关卡里的起始/答案姿态仍按“偏航后俯仰”记录。球半径 30 cm，物件都在中心 26 cm 以内；球心最低 30 cm，球不会陷进桌面。
- **关卡**（日常物件，难度递增）：

| 关卡 | 灯 | 物件 → 影子 | 答案姿态 |
| --- | --- | --- | --- |
| **BIRTHDAY** | 左墙 | 盘子、两个礼物盒（蛋糕层）、三根蜡烛、派对帽、气球 → 生日蛋糕和一只气球 | 偏航 30°，后仰 20° |
| **BON VOYAGE** | 右墙 | 卷起的地图（机身）、两张登机牌（近处长机翼、远处短机翼）、两个行李牌（尾翼）、墨镜、拍立得相机 → 起飞的飞机（机头朝右上） | 偏航 −40°，前倾 50° |
| **GOOD MORNING** | 左墙 | 平底锅（身体）、鸡蛋（头）、两颗草莓（鸡冠）、奶酪（喙）、锅铲/打蛋器/汤勺（尾羽）、两把叉子（脚）→ 公鸡 | 偏航 110°，后仰 35° |
| **HOME** | 两面墙 | 打开倒扣的书、靠垫、故事书、拖鞋、钥匙、蜡烛 → 左墙一颗心，右墙一座房子（“家是心之所在”） | 偏航 0°，俯仰 0° |

- **判定**：每面墙沿用第一轮的方法（光锥内可见影子按质心与 RMS 半径归一化，1 格容差，覆盖 ≥ 90%，多余 ≤ 10%，IoU 下限），所有用到的灯必须同时合格，并以最弱的那面墙为准；松手静止 0.5 秒完成。自由转动后图案还可能在墙上“歪着”，所以答案图案按 −10°、−5°、0、5°、10° 五个倾角各比一次，先取合格的倾角，再取 IoU 最高的（`Torch2Projection.Tilts`，Python 的 `TILTS` 与之相同，导入时校验）。IoU 下限默认 0.62；BIRTHDAY 设为 0.70（1000 个随机朝向里有 1 个歪着的“盒子 + 气球”到了 IoU 0.65）；HOME 的心和房子都是块状图形，随机朝向也能到 IoU 0.80，所以设为 0.88。实测（Python 设计脚本）窗口，绕竖直轴 / 右轴 / 灯的视线轴（图案在墙上原地转）：BIRTHDAY −6…+9° / −9…+12° / ±18°；BON VOYAGE ±18° / ±12° / ±21°；GOOD MORNING −6…+9° / ±12° / ±15°；HOME ±9° / −15…+18° / 两盏灯各 ±12°；平移 6 cm 基本都能通过（GOOD MORNING 6 个方向中 5 个）；每关 1000 个均匀随机朝向中，离答案 30° 以上的都不误判。
- **双灯关的做法**：参考 Mitra & Pauly《Shadow Art》(2009)。两盏灯各自通过目标图案形成“影子锥”，二者的交集就是影子壳（shadow hull）；任何完全在壳内的物体，两面墙上的影子都落在各自图案内。`build_torch2.py` 体素化影子壳后，用论文第 4 节的“基于样例的空间填充”：贪心地把日常物件放进壳内（候选位置瞄准尚未覆盖的像素，图案轮廓像素权重更高），再逐个取出重放做两轮优化。房顶的斜线靠一个手放的锚点：一本倒扣打开的书，从右灯看是“∧”屋顶，从左灯看是心形上部里的一小块。论文提到两张图案的高度轮廓要一致才可能同时成立，所以选了心和房子这类块状图形；试过“太阳 + 月牙”：月牙能拼出来，但太阳的细光芒用块状物件拼不出来。
- **输入**：Quest 任一只手在球心 10 cm 内捏合 = 搬动（只平移，不转）；在 10–36 cm（球壳附近）捏合 = 滚动：每帧取“球心 → 捏合点”的方向变化，把球转同样的角度（arcball），捏住的那一点始终跟着手指，任何方向都能转，可以松手再捏接着转。手靠近时环（滚）或中心小球（搬）会亮起，提示捏下去会抓到哪一个。捏合点是拿起时固定在手上的一点，只用手的位置，不用手腕的朝向。单帧移动超过 12 cm 或转动超过 30° 视为追踪跳变，跳过这一帧，跳回来的那一帧也跳过；离球心不到 5 cm 时方向不稳定，也跳过。另一只手可接手；追踪丢失时球留在原处。桌面：左键拖动 = 在光标下滚球（光标在球外时取球的轮廓点），右键拖动 = 在视平面内平移，滚轮 = 前后，A/D 绕竖直轴、W/S 前后翻、Z/C 绕视线转，方向键、Q/E 平移，H 提示。HINT 显示提示文字，并在左右两侧显示各面墙的目标图案卡片。
- **显示与性能**：灯不动，所以每面墙的光斑亮度只在加载时算一次，墙面纹理（128×120，平滑渐变，低分辨率无妨）只画灯光。影子是一个网格：把物堆朝向灯的三角形投影到墙面、裁剪到墙框内，贴在墙前 3 mm，用“同一面墙、灯光挡掉 90%”的纹理着色，所以任何距离都锐利（4x MSAA），重叠三角形颜色一致，光斑外的影子与暗墙同色。物堆动了的那一帧才重建（编辑器内两面墙合计约 0.25–0.3 ms）。评分仍用 128×120 光栅，10 Hz 一次（约 0.5–0.7 ms）；EditMode 测试核对网格与光栅在像素中心一致。物堆用细线连到中心小球，看起来是一件装置作品；细线小于一个墙像素，球的三道环和中心小球都不计入影子。
- 直接进入：**MetaHackthone > Shadow Workshop > Play Torch Levels 2**（`Scenes/TorchLevels2.unity`，初始关卡 BIRTHDAY，只在第二轮四关间循环；不加入 build scenes）。重建用 **Create torch-levels-2 scene**。

管线：`Tools/ShadowArt/build_torch2.py`（复用前两个脚本的基础体）→ `Art/Torch2/Torch2Objects.json`，**Import torch levels 2** 生成 `Art/Torch2/Meshes`、`Art/Torch2/Materials`、`Resources/ShadowArt/Torch2/<id>.asset`（`Torch2Level`）。房间常量在 Python 与 `Torch2Projection` 两边各写一份，导入时会校验，不一致就拒绝导入。单灯关在“墙面画面”里摆放（同第一轮，旋转改为相对灯的视线坐标系）；双灯关用 `--seeds 5,9,…` 试多个随机种子，取两面墙中较差者最好的一个，结果写回 `seed`。运行时：`Torch2Level`、`Torch2Projection`、`Torch2Workshop` / `Torch2World` / `Torch2Input`，房间（桌子、墙、灯、球）在 `Torch2Room`，游戏和练习场共用。`Torch2Level` 的答案和起始姿态是完整旋转（`solution`、`start`，四元数）；JSON 里仍写偏航/俯仰，导入时转换。物件另有 `scale`、`shadowMesh`（可读的投影网格，空则用 `mesh`）、`visualOnly`；关卡级 `bothFaces` 让三角形两面都投影。未在真机验证：滚球的手感（球的大小、1:1 是否够快）、手抖下 0.5 秒保持、墙角布局在 Quest 视场里是否需要转头。

## 影子练习场 / Shadow Gym

给设计师手工搭第二轮关卡用：摆物件、看影子、检查、导出、试玩，全程不用写代码，看影子也不用进 Play。

- **打开**：**MetaHackthone > Shadow Workshop > Gym > Open gym**（`Scenes/ShadowGym.unity`，不在 build 里；没有就新建一个，里面是一个蘑菇示例）。场景里 **Shadow Gym**（`Torch2Gym`）下面的 **Cluster (the orb)** 就是球：它的子物体怎么摆，就是答案（球不转时）。房间、墙、灯和游戏里完全一样，墙上的锐利影子在编辑模式下实时更新。
- **摆法**：只移动、旋转、缩放 cluster 的子物体。cluster 自己固定在静止位（拖走会弹回）；转动 cluster 只是预览球转了以后的样子，**Show answer** 转回答案。任何来源的网格都行（商店资源、FBX、预制体，可以有层级）；LOD 组只用 LOD0，蒙皮网格会跳过（先烘焙成静态网格）。
- **标记哪些参与图案**：默认 cluster 下所有网格都投影。右键物体 **Shadow Gym > Visual only** 只显示不投影（装饰），**Picture** 改回投影（作用于该物体和子物体，子物体可以自己再标）。`ShadowPiece` 上的 **Shadow Proxy** 可以换一个更简单（或为图案微调过）的网格来投影，轴心和缩放要和原网格一致。
- **Inspector 面板**：
  - **Look from left / right lamp**：Scene 视图相机放到灯的位置朝球看，看到的轮廓就是墙上的影子，最直观的摆法。
  - **Recenter pieces**：把图案物件的包围盒中心移到轴心。**Fit inside orb**：整体缩小到 36 cm 抓取范围内。
  - 统计：投影三角形数（预算 4000：球每动一帧都要重算影子）、离轴心最远的距离（36 cm），以及收集时的提示（蒙皮、倾斜缩放等）。
  - **What the game scores**：每面墙在答案时被评分的 64×64 图（去掉了位置和大小）。比这个细的细节（绳子、细线）不计分，别靠它们。
  - 转动 cluster 时显示这个姿态相对答案的 IoU / 覆盖 / 多余和是否通过，可以直接摸到容差。
- **Check level**（导出前会自动跑一遍，同 `build_torch2.py` 的检查）：
  - 关卡 id 必须是字母开头的字母/数字/下划线，且不能是 Python 生成的关卡名。至少一个投影物件。答案时每面用到的墙的光圈里都要有影子。
  - 1000 个均匀随机朝向（离答案 ≥ 30°）：最接近的 IoU + 0.05。再从离答案 ≥ **Lookalike Angle**（默认 50°）的随机朝向里挑最接近的 10 个，小步转动到各自最像的姿态：随机朝向常常错过这种姿态，玩家却能转到，典型是整堆转过去的镜像。最像的 IoU + 0.03。两者取高作为通过阈值（0.62–0.95，**Auto Threshold**；这一步只在练习场有，`build_torch2.py` 没有）。仍然通过的朝向会报警告（图案本身能读就无妨）；关掉 Auto 时，如果手填的阈值让这种姿态也能过关，会提示该填多少。
  - 容差：绕上、右、前和每盏灯视线（图案在墙上倾斜）各轴还能通过的角度（3° 步长），任一轴小于 6° 会警告（手拿不稳）。平移 6 cm 是否仍通过。
  - **翻转也算（Flips）**：默认图案上下颠倒（绕灯的视线转半圈）也算答案，影子就是原图转 180°，是精确目标（`Torch2Projection.Answers`）。左右镜像（绕图案的竖直方向转半圈）要单独勾选：灯有透视，转半圈后近大远小对调，影子只是“接近”镜像（Birthday 的镜像姿态“根本不像”，2026-11-08 起不算）。双灯关：只有上下颠倒时没有对应的半圈（两面墙不能同时倒过来），镜像是绕竖直轴半圈；两种都勾时，再加绕任一盏灯视线的半圈。上面所有“离答案多远”都按最近的答案（含翻转）算，翻转姿态不再被当成需要压下去的“像答案的姿态”。Python 生成的四关不开翻转（`build_torch2.py` 不知道翻转）。
  - 起始姿态（**Auto Start**）：在离所有答案（含翻转）≥ 70°、每面墙都有影子的朝向里选最“冷”的。也可以自己转好球按 **Use orb rotation as start**，或取消 Auto 直接填。
  - 警告还包括：三角形超预算、物件超出抓取范围、不是 URP 的材质（真机会显示粉色）、答案时影子有超过 3% 落在灯光外或超出墙面（图案被切掉，用 **Recenter** 或把物件挪离灯远一点）。
- **桌面摆放 / Table layout**：关卡开始前物件在桌上怎么摆。在 Shadow Gym 下放一个空物体（例如 `Table layout`），里面给 cluster 里的每个物件放一个**同名**的副本，摆在桌上（桌面 y = 0），然后把它拖到 Inspector 的 **Table Layout** 字段。配对按名字：先找 cluster 下最外层能配上的父物体（一个预制体有多个网格时，整体当一个物件移动），重名的按顺序配。
  - **Play 模式**：进 Play 后，桌面副本隐藏，cluster 里的物件从桌面位置一个一个飞进球里（和游戏开场一样：先垂直抬起越过桌上所有东西，绕开已经拼好的部分，再沿一条没有阻挡的线进位），灯随后亮起；Inspector 的 **Replay table to orb** 重播。默认拼成关卡的起始姿态，勾 **Transit To Answer** 拼成答案。
  - **Transit Fly**（每个物件飞多少秒，默认 0.8）和 **Transit Overlap**（下一个提前多少起飞，0 = 严格一个接一个，默认 0.3）一起导出到关卡。顺序是倒着规划的（从拼好的球里一个个拆出来，最外层先拆），所以压在下面的底座会先飞进去。
  - **Check level** 会用真实形状（凸包）每秒 30 次检查飞行中有没有穿插，报告“谁在第几秒插进谁多少厘米”；静止时本来就贴在一起的两个物件不算。没有桌面摆放的关卡由游戏自动散放。
- **Export**：写 `Resources/ShadowArt/Torch2/<id>.asset`（再次导出原地更新，引用不变），桌面摆放也一起导出（每个物件的桌面位置、朝向和分组）。Read/Write 关着的网格（商店资源大多如此）会生成一份只含位置和三角形的可读投影副本，放在 `Art/Torch2Gym/<id>/`，原资源和它的导入设置都不动。答案就是“不转”，导出的关卡都开 `bothFaces`（单面网格和负缩放的物件从背面也投影）。
- **Play this level**：导出、保存场景，然后在 `TorchLevels2` 场景里只玩这一关；退出 Play 自动回到练习场。
- **Run on headset**：导出，然后在头显上直接从这一关打开 Shape of Memory（用 **MetaHackthone > Build and Run** 里选的头显）。每次都会重新构建（导出会重写关卡文件；第一次几分钟，之后一两分钟）。不是书里四章之一的关卡单独成一本书，进度互不影响。详见 [Build and Run](BuildAndRun.md)。
- **放进 Shape of Memory**：导出后在 **MetaHackthone > Pico Final > Chapters** 里把关卡拖进某一章（拖动可调顺序）。章号、颜色和教学跟着位置走，进度按关卡 id 存。规则和检查见 [Shape of Memory · Chapters](ShapeOfMemory.md#chapters)。
- **Open level**：任何 `Torch2Level` 都能载回练习场继续改（Project 窗口右键 **Open in Shadow Gym** 也行），包括 Python 生成的四关：物件按答案摆好（球不转就是答案），文字、灯、起始、阈值、桌面摆放（有的话，重建成 `Table layout`）都带过来；**Auto Threshold** 保持打开，Check 按换过物件后的新物堆重新算阈值（以前会关掉 Auto，旧关卡的阈值留给新物堆，转错角度也能过关）。起始姿态照旧保留，变“热”了 Check 会警告；Python 关卡的 id 会加 `Gym` 后缀，避免被 **Import torch levels 2** 覆盖。
- 还没有的：导出的关卡不会自动加入游戏里的 PREV / NEXT 列表（`ShadowDemo.Levels`，需要手动加）；参考图叠加、自动简化代理网格、网格微调工具都留待以后。

## 接近提示 / Closeness cue

第二轮关卡（`Torch2Level`，含练习场导出的关卡）不用图标，用画面和声音告诉玩家离答案多近。WARMTH 百分比照旧显示。

- **画面：灯是一台没对焦的幻灯机。** 每盏灯在自己的光斑里投一张“幻灯片”（`leftSlide` / `rightSlide`）：远离答案时模糊、发灰、很淡，越接近越清晰、越有颜色，找到时完全清楚。只在光斑内显示，亮度压在 55–100%，影子（同一面墙 10% 的光）始终是墙上最暗的东西，所以不影响看图。双灯关每面墙按自己的分数对焦，哪面墙落后一眼就能看出来。实现：`Resources/ShadowArt/ProjectedSlide.shader`（URP 无光照 + 采样较粗的 mip 做模糊，单通道实例化，Quest / Pico 都适用），不用后处理，几乎不增加 GPU 开销。
- **声音：分层循环。** `layers[0]` 一直播放，其余每升一级加入一层，同时低通滤波从约 450 Hz 打开到全频（声音版的“对焦”）；升级时有一声轻提示音。声音从亮着的墙那边传来。所有层必须等长，同一采样点开始播放，才能保持同步。
- **分级，不是线性。** 阈值按 warmth（`tiers`，默认 0.35 / 0.60 / 0.80），越往上越密；通过 = 第 4 级。进入一级要达到阈值，退出要低于阈值 0.08，并且要保持一会儿（升 0.25 s，降 0.6 s），再用缓动过渡，手抖（每度 0.01–0.08 warmth）不会让提示闪烁。离答案超过 `lookalikeAngle`（50°）时最多到第 1 级：单灯关里 warmth ≥ 0.6 的随机朝向大多是离答案很远的镜像 / 翻转，不该“越来越清楚”。声音更严格：离最近的答案超过 `cueAngle`（默认 30°）就保持最冷（第 0 级，不加层、不响提示音），2026-11-08 试玩反馈远处的“像答案的姿态”让音乐变响、误导玩家。
- **松手吸附。** 松手时如果已经通过，或 warmth ≥ `snapWarmth`（0.90）且离答案不超过 `snapAngle`（20°），球会在 0.25–0.5 s 内自己转到准确答案（只转不移），然后照常静止 0.5 s 完成。吸附到最近的答案（含翻转）；在翻转答案上完成后，球再用约 1.4 s 自己转回正确方向，墙上的图案随之摆正。拿在手里时，这里（练习场、TorchLevels2）从不吸附；吸附中再捏住，手说了算。**Shape of Memory** 还会在手里吸附（`Torch2Workshop.SnapWhileHeld`）：拿着转到离任一答案 `heldSnapAngle`（默认 8°）以内，或两次采样之间转过了答案，球就离手、转到答案并直接算找到（试玩反馈：拿着转过头就不算）。
- 实测（练习场 **Check level** 会给每关报这组数字）：

| 关卡 | 随机朝向达到 1 / 2 / 3 级 | 典型转动从多远开始到达 1 / 2 / 3 级 | 松手吸附范围 |
| --- | --- | --- | --- |
| BIRTHDAY | 15% / 1.0% / 0.3% | 约 40° / 30° / 15° | 约 10° |
| BON VOYAGE | 12% / 0.8% / 0.7% | 约 40° / 30° / 20° | 约 20° |
| GOOD MORNING | 11% / 0.3% / 0% | 约 40° / 20° / 10° | 约 10° |
| HOME | 3.2% / 0.5% / 0.2% | 约 30° / 20° / 15° | 约 15° |

- **素材（目前是生成的占位）**：幻灯片留空就用每关每面墙生成的占位图（天际线 + 色块，模糊后只剩色块，清楚后才看得见窗户）；声音层留空就用生成的 4 层 8 秒循环（A 调：持续音、拨弦、钟声、旋律）。导入正式素材：
  - 幻灯片：任意尺寸（建议 1024 左右，接近 16:15，会居中裁切铺满墙面），导入设置里必须勾 **Generate Mipmaps**（没有 mip 就模糊不了，Check 会警告）。中间区域避免强对比，影子要叠在上面读。
  - 声音层：同一长度、首尾无缝的循环，第一层是一直在的底层，后面按“越近越加”的顺序排；`cueVolume` 调整整体音量。层数不限，其余层平均分到第 1–3 级。
  - 在练习场里填（**Closeness cue** 一栏，导出 / Open in gym 都会带上），或直接改 `Resources/ShadowArt/Torch2/<id>.asset`。**Import torch levels 2** 不会覆盖这些字段。**Preview Tier**（0–4）在编辑模式下直接看各级的幻灯片效果；声音在 **Play this level** 里听。
- **Check level** 新增：每级被随机朝向达到的比例、典型转动从多远开始到达、松手吸附范围；第 1 级超过 35% 的随机朝向（太容易）、第 3 级在 10° 以内才出现（太晚）、幻灯片没有 mip、声音层长度不一致都会警告。
- 实测表是 2026-11-08 之前的老四关数字。四章现在只开上下颠倒（Home 是双灯关，所以没有翻转）。阈值：Birthday_NEW 0.76、BonVoyage_NEW 0.74、GoodMorning_NEW 0.86、Home_NEW 0.62；镜像那一侧的“像答案的姿态”要从镜像半圈姿态起爬坡才找得到，随机朝向常常错过（GoodMorning 曾在 174° 找到 0.825）。Birthday_NEW 的盘子、两个礼盒、中间那根蜡烛和派对帽沿灯光射线挪了位置并按比例缩放（答案影子逐像素不变），转到背面时不再像蛋糕：背面最像的姿态 IoU 从约 0.75–0.82 降到 0.69（167°）。
- 代码：`Torch2Cue`（分级 `Torch2Tier`、画面和声音）、`Torch2Placeholders`（占位素材）、`Torch2Workshop.TrySnap` / `HeldSnap`、`Torch2Projection.Answers` / `Turns`、`Torch2Room.Slide` / `Focus`。测试：EditMode `Torch2CueTests`，PlayMode `Torch2CuePlayTests`。未在真机验证：模糊在头显里的观感、声音的空间感和音量、手抖下的吸附手感。

## Shape of Memory

第二轮四关的正式游戏版（标题、选关、暂停、通关卡片、实体按钮、存档）在 `Assets/ShadowWorkshop/PicoFinal`，说明见 [ShapeOfMemory.md](ShapeOfMemory.md)。它以 `Torch2Workshop.bare` 运行关卡，不显示上面的调试界面。

## 接手开发

- `Assets/ShadowWorkshop/Runtime`：原型关卡规则、真实三角形投影、掩模评分、抓放和灰盒显示。保留 `CloudGarden.ShadowPuzzle` 命名空间，独立 `ShadowWorkshop.Runtime` assembly。
- `Assets/ShadowWorkshop/Integration`：共享玩家适配、入口和关内切换。以后换手势或调整摆台位置优先修改这里。
- `Assets/ShadowWorkshop/Resources/ShadowArt/ThreeWishes.json`：三愿烘焙几何，运行时不依赖 Python。
- `Tools/ShadowArt/build_three_wishes.py`：几何生成器，需要 numpy、scipy、Pillow；写入上述 JSON，预览写到 `Logs/ShadowArt`。生成后须重跑投影测试。
- `Assets/ShadowWorkshop/Editor/ShadowDemoBuilder.cs`：用 Unity Editor API 生成场景，保留已有渲染、XR、包版本和构建设置。
- `Assets/ShadowWorkshop/Tests`：迁入的投影/手势回归，以及共享吸附、三阶段流程、关内切换、单一玩家和追踪丢失验证。

当前仍是原型视觉和英文界面。投影评分复用显示掩模，不依赖实时灯光阴影或 GPU 回读。最终连通性沿用带容差的 AABB 检查，不是精确接触求解。

## 验证

项目版本保持 **Unity 6000.3.21f1**。可运行：

```powershell
unity run . --editor-version 6000.3.21f1 -- -buildTarget Win64 -executeMethod ShadowWorkshopIntegration.Editor.ShadowDemoBuilder.Build -logFile Logs/Shadow-import.log
unity test . --mode EditMode --output Logs/Shadow-EditMode.xml -- -buildTarget Win64 -logFile Logs/Shadow-EditMode.log
unity test . --mode PlayMode --output Logs/Shadow-PlayMode.xml -- -buildTarget Win64 -logFile Logs/Shadow-PlayMode.log
```

2026-10-19 在原总项目中验证：EditMode **126/126** 通过（`Logs/Shadow-EditMode.xml`）；最终完整 PlayMode **55/55** 通过（`Logs/Shadow-PlayMode-Final.xml`）。三愿完整揭晓、松手门槛、当前累计约束、实际按钮切换三关、持有时禁切关、一个玩家及追踪丢失不吸附均通过。

Windows 开发预览构建成功，**0 errors**（`Logs/Shadow-WindowsBuild-Clean.log`），（原总项目当时的六个启用场景）。输出 `Builds/ShadowWorkshop/MetaHackathon.exe`；分发时需要整个目录。可用 **MetaHackthone > Shadow Workshop > Build Windows preview** 重建。工具处理了 Meta SDK 在重复构建时不覆盖生成的 `RuntimeActionBindings.json` 的问题。构建和测试日志、缓存及可执行文件不提交到仓库。

Quest 真机的手指舒适度、遮挡恢复、射线/近触争用、系统手势和帧率需要佩戴设备验收，桌面结果不代表真机通过；本次没有生成或部署 APK。

![三愿完成后的玩家雕塑](ShadowWorkshopComplete.png)

几何生成思想参考 Mitra 与 Pauly 的 Shadow Art（2009）；迁入的是原型自制几何和算法，不包含论文图片或模型。
