"""
Берёт текстуры из source/, делает бесшовными, сжимает до 32x32 nearest neighbor.
Результат — в tiles/

Использование:
    pip install Pillow numpy
    python make_tiles.py
"""

from PIL import Image
import numpy as np
import os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SRC_DIR = os.path.join(SCRIPT_DIR, "source")
DST_DIR = os.path.join(SCRIPT_DIR, "tiles")
TILE_SIZE = 32
INTERMEDIATE = 128  # промежуточный размер для seamless blend


def make_seamless(img_array):
    """Делает текстуру бесшовной через сдвиг на половину + косинусную маску.
    
    1. Сдвигаем картинку на w/2, h/2 — старые швы (края) попадают в центр.
    2. Косинусная маска: 0 на краях, 1 в центре.
    3. Края берём из сдвинутой (там был интерьер — швов нет),
       центр из оригинала (там тоже интерьер — швов нет).
    """
    h, w, c = img_array.shape
    src = img_array.astype(np.float32)

    # Сдвиг на половину — швы оригинала теперь в центре
    shifted = np.roll(np.roll(src, w // 2, axis=1), h // 2, axis=0)

    # Raised-cosine маска: 0 на краях, 1 в центре
    xs = np.linspace(0.0, 2.0 * np.pi, w, endpoint=False)
    ys = np.linspace(0.0, 2.0 * np.pi, h, endpoint=False)
    xg, yg = np.meshgrid(xs, ys)
    alpha = ((0.5 - 0.5 * np.cos(xg)) * (0.5 - 0.5 * np.cos(yg)))[:, :, np.newaxis]

    result = shifted * (1.0 - alpha) + src * alpha
    return np.clip(result, 0, 255).astype(np.uint8)


def make_tiled_preview(tile_path, repeats=4):
    """Создаёт превью тайла повторённого repeats x repeats раз для проверки швов"""
    tile = Image.open(tile_path)
    w, h = tile.size
    preview = Image.new('RGBA', (w * repeats, h * repeats))
    for y in range(repeats):
        for x in range(repeats):
            preview.paste(tile, (x * w, y * h))
    return preview


def process():
    if not os.path.exists(SRC_DIR):
        os.makedirs(SRC_DIR)
        print(f"Создана папка: {SRC_DIR}")
        print(f"Положи туда исходные текстуры (grass.png, dirt.png, stone.png, sand.png, snow.png)")
        print(f"Потом запусти скрипт снова.")
        return

    os.makedirs(DST_DIR, exist_ok=True)
    preview_dir = os.path.join(SCRIPT_DIR, "preview")
    os.makedirs(preview_dir, exist_ok=True)

    files = [f for f in os.listdir(SRC_DIR)
             if f.lower().endswith(('.png', '.jpg', '.jpeg', '.webp'))]

    if not files:
        print(f"Папка {SRC_DIR} пуста!")
        print(f"Положи туда текстуры и запусти снова.")
        return

    for f in files:
        src_path = os.path.join(SRC_DIR, f)
        img = Image.open(src_path).convert('RGBA')
        original_size = img.size

        # Уменьшаем до промежуточного размера (LANCZOS — качественное сжатие)
        img = img.resize((INTERMEDIATE, INTERMEDIATE), Image.LANCZOS)

        # Делаем бесшовной
        arr = np.array(img)
        arr = make_seamless(arr)

        # Сжимаем до 32x32 nearest neighbor — пиксельный стиль
        img = Image.fromarray(arr)
        img = img.resize((TILE_SIZE, TILE_SIZE), Image.NEAREST)

        out_name = os.path.splitext(f)[0] + '.png'
        out_path = os.path.join(DST_DIR, out_name)
        img.save(out_path)

        # Создаём превью 4x4 для проверки швов
        preview = make_tiled_preview(out_path)
        preview.save(os.path.join(preview_dir, f"check_{out_name}"))

        print(f"  {f} ({original_size[0]}x{original_size[1]}) -> {out_name} ({TILE_SIZE}x{TILE_SIZE})")

    print(f"\nГотово!")
    print(f"  Тайлы:  {DST_DIR}")
    print(f"  Превью: {preview_dir}  (проверь швы)")


if __name__ == '__main__':
    process()
