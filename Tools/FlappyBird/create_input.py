"""Create stable action IDs once; reruns leave user bindings intact."""
import json
import pathlib
import uuid

root = pathlib.Path(__file__).resolve().parents[2]
path = root / 'Assets/Flight/Input/FlappyFlight.inputactions'
path.parent.mkdir(parents=True, exist_ok=True)
def identifier(name):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, 'flappy-flight-prototype/' + name))
actions, bindings = [], []
def action(name, kind, expected):
    actions.append(dict(name=name, type=kind, id=identifier(name), expectedControlType=expected,
                        processors='', interactions='', initialStateCheck=kind == 'Value'))
def binding(name, control, part='', composite=False):
    bindings.append(dict(name=part, id=identifier(name + control + part), path=control,
                         interactions='', processors='', groups='KeyboardMouse', action=name,
                         isComposite=composite, isPartOfComposite=bool(part) and not composite))
for name, negative, positive in [('Pitch', 's', 'w'), ('Roll', 'a', 'd'), ('Yaw', 'q', 'e')]:
    action(name, 'Value', 'Axis')
    binding(name, '1DAxis', 'Axis', True)
    binding(name, '<Keyboard>/' + negative, 'negative')
    binding(name, '<Keyboard>/' + positive, 'positive')
action('Look', 'PassThrough', 'Vector2')
binding('Look', '<Mouse>/delta')
for name, paths in [('Faster', ['<Keyboard>/leftShift', '<Keyboard>/rightShift']),
                    ('Slower', ['<Keyboard>/leftCtrl', '<Keyboard>/rightCtrl']),
                    ('FreeLook', ['<Keyboard>/c']), ('Reset', ['<Keyboard>/r']),
                    ('ReleaseCursor', ['<Keyboard>/escape']), ('CaptureCursor', ['<Mouse>/leftButton'])]:
    action(name, 'Button', 'Button')
    for control in paths:
        binding(name, control)
document = dict(name='FlappyFlight', maps=[dict(name='Flight', id=identifier('Flight'), actions=actions, bindings=bindings)],
                controlSchemes=[dict(name='KeyboardMouse', bindingGroup='KeyboardMouse',
                                     devices=[dict(devicePath='<Keyboard>', isOptional=False, isOR=False),
                                              dict(devicePath='<Mouse>', isOptional=False, isOR=False)])])
if not path.exists():
    path.write_text(json.dumps(document, indent=2), encoding='utf-8')
print(path)
