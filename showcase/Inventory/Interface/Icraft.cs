using Unity.Netcode;
using UnityEngine;

public interface ICraft
{
    itemType getItem(NetworkObjectReference refObj, bool isAsk);
    bool putItem(itemType item, GameObject obj);
}
