from targets import BUILD

ALPHA = BUILD == "36731"
FIRST_EQUIPPED_ACTION_LIST_ID = {"36731": 1262, "38328": 26693}[BUILD]


def _crc_table():
    table = []
    for i in range(256):
        c = i << 24
        for _ in range(8):
            c = ((c << 1) ^ 0x04C11DB7) & 0xFFFFFFFF if c & 0x80000000 else (c << 1) & 0xFFFFFFFF
        table.append(c)
    return table


CRC_TABLE = _crc_table()


def crc(name):
    c = 0
    for b in name.lower().encode():
        c = CRC_TABLE[((c >> 24) ^ b) & 0xFF] ^ ((c << 8) & 0xFFFFFFFF)
    return c


def action_lists(action):
    area = action.get("AreaOfEffect", {}).get("ContextCollectionParams", {}).get("Actions", {})
    return [area, action.get("OnActiveWindowEntered", {}), action.get("OnActiveWindowExited", {})]


def equipped_action_lists(geo):
    actions = geo["EquippedActions"]
    order = sorted(actions, key=lambda a: a["Index"] if ALPHA else crc(a["Name"]))
    for rank, action in enumerate(order):
        for slot, actions_list in enumerate(action_lists(action)):
            yield FIRST_EQUIPPED_ACTION_LIST_ID + 3 * rank + slot, slot, action, actions_list


def list_entries(actions_list, key):
    if ALPHA:
        return [entry[key[1]] for entry in actions_list.get(key[0], [])]
    return [entry["Action"] for entry in actions_list.get("ActionArray", [])]
