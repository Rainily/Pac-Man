"""
train_ghost_ai.py

Trains a tiny MLP to play "ghost" in the Pac-Man maze via imitation
learning: it samples random game states, computes the "teacher" action
using the same chase/flee distance heuristic as GhostController.cs, and
trains a network to reproduce that decision. The result is a drop-in
neural replacement for ChooseDirection() with the same behavior as a
starting point -- you can then fine-tune it, add reward-based RL, or
give different ghosts differently-trained models for distinct personalities.

Requirements:
    pip install torch onnx

Run:
    python train_ghost_ai.py

Output:
    ghost_ai.onnx   -- import this into Unity's Assets folder
"""

import random
import torch
import torch.nn as nn
import torch.nn.functional as F

# ---------------------------------------------------------------------------
# 1. Maze definition -- MUST match the `layout` array in MazeGenerator.cs
# ---------------------------------------------------------------------------
LAYOUT = [
    "###############",
    "#......#......#",
    "#o##.#.#.#.##o#",
    "#.............#",
    "#.##.#.###.##.#",
    "#....#..G#....#",
    "###.####.####.#",
    "#....#P..#....#",
    "#.##.#.#.#.##.#",
    "#o.#.....#..o.#",
    "###############",
]
ROWS = len(LAYOUT)
COLS = len(LAYOUT[0])

WALKABLE = set()
for r in range(ROWS):
    for c in range(COLS):
        if LAYOUT[r][c] != "#":
            WALKABLE.add((c, r))

# Direction order MUST match the C# side: 0=up, 1=down, 2=left, 3=right
# Note: in Unity's world space "up" is +Y, but our grid row 0 is the top,
# so moving "up" on screen means row - 1. Keep this consistent everywhere.
DIRS = [(0, -1), (0, 1), (-1, 0), (1, 0)]  # up, down, left, right


def is_walkable(cell):
    return cell in WALKABLE


def random_walkable_cell():
    return random.choice(tuple(WALKABLE))


# ---------------------------------------------------------------------------
# 2. Teacher heuristic -- mirrors GhostController.ChooseDirection() in C#
# ---------------------------------------------------------------------------
def teacher_action(ghost, player, current_dir_idx, power_mode):
    opposite = {0: 1, 1: 0, 2: 3, 3: 2}[current_dir_idx] if current_dir_idx is not None else None

    candidates = []
    for i, (dx, dy) in enumerate(DIRS):
        if i == opposite:
            continue
        nxt = (ghost[0] + dx, ghost[1] + dy)
        if is_walkable(nxt):
            candidates.append(i)

    if not candidates:
        # dead end -- forced reverse
        return opposite if opposite is not None else random.randrange(4)

    def dist(i):
        dx, dy = DIRS[i]
        nxt = (ghost[0] + dx, ghost[1] + dy)
        return ((nxt[0] - player[0]) ** 2 + (nxt[1] - player[1]) ** 2) ** 0.5

    if power_mode:
        return max(candidates, key=dist)   # flee: maximize distance
    else:
        return min(candidates, key=dist)   # chase: minimize distance


# ---------------------------------------------------------------------------
# 3. State encoding -- MUST match BuildInputTensor() in GhostControllerNN.cs
#    [canUp, canDown, canLeft, canRight, dx, dy, powerFlag, dirOneHot(4)]
#    = 11 floats
# ---------------------------------------------------------------------------
def encode_state(ghost, player, current_dir_idx, power_mode):
    can_move = []
    for dx, dy in DIRS:
        nxt = (ghost[0] + dx, ghost[1] + dy)
        can_move.append(1.0 if is_walkable(nxt) else 0.0)

    dx = (player[0] - ghost[0]) / COLS
    dy = (player[1] - ghost[1]) / ROWS

    dir_one_hot = [0.0, 0.0, 0.0, 0.0]
    if current_dir_idx is not None:
        dir_one_hot[current_dir_idx] = 1.0

    return can_move + [dx, dy, 1.0 if power_mode else 0.0] + dir_one_hot


# ---------------------------------------------------------------------------
# 4. Dataset generation
# ---------------------------------------------------------------------------
def generate_dataset(n_samples=60_000):
    X, y = [], []
    for _ in range(n_samples):
        ghost = random_walkable_cell()
        player = random_walkable_cell()
        current_dir_idx = random.choice([None, 0, 1, 2, 3])
        power_mode = random.random() < 0.3

        label = teacher_action(ghost, player, current_dir_idx, power_mode)
        X.append(encode_state(ghost, player, current_dir_idx, power_mode))
        y.append(label)

    return torch.tensor(X, dtype=torch.float32), torch.tensor(y, dtype=torch.long)


# ---------------------------------------------------------------------------
# 5. Model
# ---------------------------------------------------------------------------
class GhostNet(nn.Module):
    def __init__(self, input_dim=11, hidden=32, output_dim=4):
        super().__init__()
        self.fc1 = nn.Linear(input_dim, hidden)
        self.fc2 = nn.Linear(hidden, hidden)
        self.fc3 = nn.Linear(hidden, output_dim)

    def forward(self, x):
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        return self.fc3(x)  # raw logits; softmax handled on the Unity side


# ---------------------------------------------------------------------------
# 6. Train
# ---------------------------------------------------------------------------
def train():
    X, y = generate_dataset()
    n = len(X)
    split = int(n * 0.9)
    X_train, y_train = X[:split], y[:split]
    X_val, y_val = X[split:], y[split:]

    model = GhostNet()
    optimizer = torch.optim.Adam(model.parameters(), lr=1e-3)

    epochs = 20
    batch_size = 256

    for epoch in range(epochs):
        perm = torch.randperm(len(X_train))
        total_loss = 0.0
        for i in range(0, len(X_train), batch_size):
            idx = perm[i:i + batch_size]
            xb, yb = X_train[idx], y_train[idx]

            optimizer.zero_grad()
            logits = model(xb)
            loss = F.cross_entropy(logits, yb)
            loss.backward()
            optimizer.step()
            total_loss += loss.item() * len(idx)

        with torch.no_grad():
            val_logits = model(X_val)
            val_acc = (val_logits.argmax(dim=1) == y_val).float().mean().item()

        print(f"epoch {epoch+1:2d}/{epochs}  loss={total_loss/len(X_train):.4f}  val_acc={val_acc:.4f}")

    return model


# ---------------------------------------------------------------------------
# 7. Export to ONNX
# ---------------------------------------------------------------------------
def export_onnx(model, path="ghost_ai.onnx"):
    model.eval()
    dummy_input = torch.zeros(1, 11, dtype=torch.float32)
    torch.onnx.export(
        model,
        dummy_input,
        path,
        input_names=["input"],
        output_names=["output"],
        opset_version=15,          # within Sentis's supported opset range
        dynamic_axes=None,         # fixed batch size of 1 keeps Sentis import simple
    )
    print(f"Exported {path}")


if __name__ == "__main__":
    trained_model = train()
    export_onnx(trained_model)
