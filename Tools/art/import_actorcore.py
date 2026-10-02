"""Unpack an ActorCore "Unity" download ZIP into Assets/ThirdParty/ActorCore/<actor>/.

Keeps the FBX (rig + facial blendshapes) and writes Standard-shader maps at 2048:
  Albedo.png (diffuse RGB + opacity alpha), Normal.png, MetallicSmoothness.png, Occlusion.png.
ActorCore content is licensed per purchaser and must not be redistributed, so the folder
is git-ignored; the Unity side falls back to Rocketbox when it is absent.

usage: python import_actorcore.py <download.zip> <unity-project-root>
"""
import io, os, re, struct, sys, zipfile
from PIL import Image

SIZE = 2048


def embedded_textures(fbx: bytes):
    """Return {basename: bytes} for Video Content blobs embedded in a binary FBX."""
    out = {}
    for m in re.finditer(rb'RelativeFilename', fbx):
        name = re.findall(rb'[\x20-\x7e]{4,}', fbx[m.end():m.end() + 300])[0].decode(errors='ignore')
        name = name.replace('\\', '/').split('/')[-1].rstrip('*')
        c = fbx.find(b'Content', m.end(), m.end() + 600)
        if c < 0:
            continue
        p = c + len(b'Content')
        if fbx[p:p + 1] != b'R':
            continue
        (length,) = struct.unpack('<I', fbx[p + 1:p + 5])
        if length > 0 and name not in out:
            out[name] = fbx[p + 5:p + 5 + length]
    return out


def fit(img):
    return img if img.size[0] <= SIZE else img.resize((SIZE, SIZE), Image.LANCZOS)


def main(zip_path, project):
    z = zipfile.ZipFile(zip_path)
    names = z.namelist()
    actors = sorted({n.split('/')[1] for n in names if n.startswith('Actor/') and n.endswith('.fbx')})
    root = os.path.join(project, 'Assets', 'ThirdParty', 'ActorCore')
    os.makedirs(root, exist_ok=True)
    for actor in actors:
        dst = os.path.join(root, actor)
        os.makedirs(dst, exist_ok=True)
        fbx = z.read(f'Actor/{actor}/{actor}.fbx')
        with open(os.path.join(dst, f'{actor}.fbx'), 'wb') as f:
            f.write(fbx)
        emb = embedded_textures(fbx)
        diffuse = next((Image.open(io.BytesIO(v)) for k, v in emb.items() if 'Diffuse' in k), None)
        normal = next((Image.open(io.BytesIO(v)) for k, v in emb.items() if 'Normal' in k), None)
        tex_set = f'Texture sets/Actor/{actor}/00/'
        if diffuse is None and tex_set + 'Diffuse.jpg' in names:
            diffuse = Image.open(io.BytesIO(z.read(tex_set + 'Diffuse.jpg')))
        if normal is None and tex_set + 'Normal.jpg' in names:
            normal = Image.open(io.BytesIO(z.read(tex_set + 'Normal.jpg')))
        albedo = fit(diffuse.convert('RGBA'))
        if tex_set + 'Opacity.jpg' in names:
            op = Image.open(io.BytesIO(z.read(tex_set + 'Opacity.jpg'))).convert('L')
            if op.getextrema()[0] < 250:
                albedo.putalpha(op.resize(albedo.size, Image.LANCZOS))
        albedo.save(os.path.join(dst, 'Albedo.png'), optimize=True)
        if normal is not None:
            fit(normal.convert('RGB')).save(os.path.join(dst, 'Normal.png'))
        pbr = [n for n in names if n.startswith(f'Actor/{actor}/textures/')]
        ma = next((n for n in pbr if n.endswith('MetallicAlpha.png')), None)
        if ma:
            fit(Image.open(io.BytesIO(z.read(ma))).convert('RGBA')).save(os.path.join(dst, 'MetallicSmoothness.png'))
        ao = next((n for n in pbr if n.endswith('_ao.jpg')), None)
        if ao:
            fit(Image.open(io.BytesIO(z.read(ao))).convert('L')).save(os.path.join(dst, 'Occlusion.png'))
        alpha = albedo.getextrema()[3][0] < 250
        print(f'{actor}: embedded={sorted(emb)} alpha={alpha} files={sorted(os.listdir(dst))}')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
