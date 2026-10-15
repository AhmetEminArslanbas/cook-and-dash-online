using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class stoveBox : NetworkBehaviour, ICraft //tabak ve yemekleri �st�ene konulmas�na izin ver ama pi�irme
{
    [SerializeField] List<objectType> holdItems = new List<objectType>();
    [SerializeField] itemType cookItem;

    GameObject canvasObj;
    Image stoveImage;

    [SerializeField] float cookTime, stoveTime;

    /// <summary>
    /// Pisirme suresi merkezi dengeden alinir.
    ///
    /// Prefab'daki deger (3 sn) tek basina ayarlanmisti; sabir, spawn araligi ve
    /// kesme suresi ile iliskisi yoktu. Artik butun sureler gameBalance uzerinden
    /// birlikte ayarlaniyor (bkz. gameBalance).
    /// </summary>
    void applyBalance()
    {
        stoveTime = gameBalance.PISIRME;
    }
    
    bool isCook;

    

    private void Awake()
    {
        applyBalance();

        canvasObj = transform.GetChild(0).gameObject;
        stoveImage = canvasObj.transform.GetChild(1).GetComponent<Image>();

        canvasObj.GetComponent<Canvas>().worldCamera = Camera.main;
        dunyaGostergesi.netlestir(canvasObj);
        canvasObj.SetActive(false);

        cookItem = itemType.NONE;
    }

    void LateUpdate()
    {
        dunyaGostergesi.kamerayaCevir(canvasObj);
        sfx.dongu(this, sfxType.SIZZLE, isCook && cookItem != itemType.NONE);
    }

    /// <summary>Bolum biter bitmez ocagi bos haline getirir (sunucuda); bkz. tableManager.bolumSonuTemizligi.</summary>
    public void bolumSonuSifirla()
    {
        if (IsServer && IsSpawned)
            leaveItemClientRpc(GetComponent<NetworkObject>());
    }


    internal itemType testCookItem() { return cookItem; }
    internal bool testGostergeAcik() { return canvasObj.activeSelf; }

    public itemType getItem(NetworkObjectReference networkObj, bool isAsk)
    {
        if (!isAsk)
        {
            if (cookItem != itemType.NONE)
            {
                itemType rCook = cookItem;
                leaveItem();
                return rCook;
            }
        }
        return cookItem;
    }

    public bool putItem(itemType item, GameObject obj)
    {
        if (cookItem == itemType.NONE && 
            (item == itemType.MEAT || 
            item == itemType.COOKED_MEAT || 
            item == itemType.MEATBALL || 
            item == itemType.COOKED_MEATBALL)) //pi�irilebilen yekemler
        {
            setEnableServerRpc(GetComponent<NetworkObject>(), item);

            /*foreach (objectType objectType in holdItems)
            {
                if (item == objectType.itemType)
                {
                    cookItem = item;
                    
                    objectType.itemObj.SetActive(true);
                    
                    if (cookItem != itemType.BURNT_MEAT ||
                        cookItem != itemType.DISH ||
                        cookItem != itemType.DIRTY_DISH ||
                        cookItem != itemType.FULL_DISH) //yanmis veya pi�irilmeyen objeler
                    {
                        isCook = true;
                        cookTime = 0;
                        canvasObj.SetActive(true);
                    }
                }
                else
                {
                    objectType.itemObj.SetActive(false);
                }
            }*/
            return true;
        }
        else
            return false;
    }
    [ServerRpc(RequireOwnership = false)]
    void setEnableServerRpc(NetworkObjectReference refObj, itemType item)
    {
        setEnableClientRpc(refObj, item);
    }
    [ClientRpc]
    void setEnableClientRpc(NetworkObjectReference refObj, itemType item)
    {
        if (refObj.TryGet(out var networkObj))
        {
            stoveBox stoveBox = networkObj.gameObject.GetComponent<stoveBox>();

            foreach (objectType objectType in stoveBox.holdItems)
            {
                if (item == objectType.itemType)
                {
                    stoveBox.cookItem = item;

                    objectType.itemObj.SetActive(true);

                    if (stoveBox.cookItem != itemType.BURNT_MEAT ||
                        stoveBox.cookItem != itemType.BURNT_MEATBALL ||
                        stoveBox.cookItem != itemType.DISH ||
                        stoveBox.cookItem != itemType.DIRTY_DISH ||
                        stoveBox.cookItem != itemType.FULL_DISH) //yanmis veya pi�irilmeyen objeler
                    {
                        stoveBox.isCook = true;
                        stoveBox.cookTime = 0;
                        stoveBox.canvasObj.SetActive(true);
                    }
                }
                else
                {
                    objectType.itemObj.SetActive(false);
                }
            }
        }
    }

    void leaveItem()
    {
        leaveItemServerRpc(GetComponent<NetworkObject>());

        /*foreach (objectType objectType in holdItems)
        {
            if(objectType.itemObj != null)
                objectType.itemObj.SetActive(false);
        }
        cookItem = itemType.NONE;
        canvasObj.SetActive(false);*/
    }
    [ServerRpc(RequireOwnership = false)]
    void leaveItemServerRpc(NetworkObjectReference refObj)
    {
        leaveItemClientRpc(refObj);
    }
    [ClientRpc]
    void leaveItemClientRpc(NetworkObjectReference refObj)
    {
        if(refObj.TryGet(out var netwokrobj))
        {
            stoveBox stoveBox = netwokrobj.gameObject.GetComponent<stoveBox>();

            foreach (objectType objectType in stoveBox.holdItems)
            {
                if (objectType.itemObj != null)
                    objectType.itemObj.SetActive(false);
            }
            stoveBox.cookItem = itemType.NONE;
            stoveBox.canvasObj.SetActive(false);

            // Ocak bosaldi: pisirme sayaci da durur. Eskiden acik kaliyor, bos ocak
            // ilerleme mesaji gondermeye devam ediyordu.
            stoveBox.isCook = false;
            stoveBox.cookTime = 0f;
        }
    }

    // ---- Her frame RPC gonderimini onleyen durum ----
    //
    // Ilerleme cubugu her karede (60 RPC/saniye) ve "sure doldu" RPC'si de isCook
    // sunucudan kapanana kadar her karede gonderiliyordu. Reliable RPC'ler transport
    // kuyrugunda birikip uzaktaki oyuncunun baglantisini kopariyordu.
    const float IMAGE_SEND_INTERVAL = 0.1f;
    float lastImageSendTime;

    bool sentTimeIsUp;

    void sendImageValue(float value)
    {
        if (value < 1f && Time.unscaledTime - lastImageSendTime < IMAGE_SEND_INTERVAL)
            return;

        lastImageSendTime = Time.unscaledTime;

        // Yerel cubuk sunucuyu beklemez; RPC digerleri icindir.
        setFillTarget(value);

        setImageServerRpc(GetComponent<NetworkObject>(), value);
    }

    private void Update()
    {
        // Yumusatma herkeste calismali; asagisi yalnizca sahibinin mantigidir.
        smoothFill();

        if (!IsOwner) return;

        if (isCook)
        {
            cookTime += Time.deltaTime;

            sendImageValue(cookTime / stoveTime);
            //stoveImage.fillAmount = cookTime / stoveTime;
            if (cookTime >= stoveTime)
            {
                if (!sentTimeIsUp)
                {
                    sentTimeIsUp = true;
                    timeIsUpServerRpc(GetComponent<NetworkObject>());
                }
            }
            else
            {
                sentTimeIsUp = false;
            }
        }
    }
    [ServerRpc(RequireOwnership = false)]
    void timeIsUpServerRpc(NetworkObjectReference refObj)
    {
        timeIsUpClientRpc(refObj);
    }
    [ClientRpc]
    void timeIsUpClientRpc(NetworkObjectReference refObj)
    {
        if(refObj.TryGet(out var networkObj))
        {
            stoveBox stoveBox = networkObj.gameObject.GetComponent<stoveBox>();

            stoveBox.isCook = false;
            stoveBox.canvasObj.SetActive(false);

            switch (stoveBox.cookItem)
            {
                case itemType.MEAT:
                    stoveBox.cookItem = itemType.COOKED_MEAT;
                    sfx.play(sfxType.DONE);
                    itemSelect(stoveBox);
                    stoveBox.isCook = true;
                    stoveBox.cookTime = 0;
                    stoveBox.canvasObj.SetActive(true);
                    break;
                case itemType.COOKED_MEAT:
                    stoveBox.cookItem = itemType.BURNT_MEAT;
                    sfx.play(sfxType.BURN);
                    stoveBox.isCook = false;
                    itemSelect(stoveBox);
                    break;
                case itemType.MEATBALL:
                    stoveBox.cookItem = itemType.COOKED_MEATBALL;
                    sfx.play(sfxType.DONE);
                    itemSelect(stoveBox);
                    stoveBox.isCook = true;
                    stoveBox.cookTime = 0;
                    stoveBox.canvasObj.SetActive(true);
                    break;
                case itemType.COOKED_MEATBALL:
                    stoveBox.cookItem = itemType.BURNT_MEATBALL;
                    sfx.play(sfxType.BURN);
                    stoveBox.isCook = false;
                    itemSelect(stoveBox);
                    break;
            }
        }
    }

    // Dolgu orani kaybolabilir veri: bir kare atlanmasi gorunmez, sonucu timeIsUp
    // belirliyor. Unreliable gonderilir, boylece kuyrukta beklemez.
    [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    void setImageServerRpc(NetworkObjectReference refObj, float value)
    {
        setImageClientRpc(refObj, value);
    }
    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    void setImageClientRpc(NetworkObjectReference refObj, float value)
    {
        if(refObj.TryGet(out var netwokrObj))
        {
            netwokrObj.gameObject.GetComponent<stoveBox>().setFillTarget(value);
        }
    }

    // ---- Pisirme cubugu yumusatma ----
    // Saniyede ~10 guncelleme gonderiliyor; dogrudan yazilinca cubuk atlaya atlaya
    // ilerliyordu. Gelen deger hedef olarak saklanip her karede o hedefe gidilir.
    float targetFill = -1f;

    void setFillTarget(float value)
    {
        if (stoveImage == null)
            return;

        if (value < stoveImage.fillAmount)
            stoveImage.fillAmount = value;

        targetFill = value;
    }

    void smoothFill()
    {
        if (targetFill < 0f || stoveImage == null)
            return;

        float speed = (stoveTime > 0f ? 1f / stoveTime : 1f) * 1.5f;
        stoveImage.fillAmount = Mathf.MoveTowards(stoveImage.fillAmount, targetFill, Time.deltaTime * speed);
    }

    void itemSelect(stoveBox stoveBox)
    {
        foreach (objectType objectType in stoveBox.holdItems)
        {
            if (objectType.itemType == stoveBox.cookItem)
            {
                objectType.itemObj.SetActive(true);
            }
            else
                objectType.itemObj.SetActive(false);
        }
    }
}
