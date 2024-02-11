using System;
using System.Collections;
using System.Collections.Generic;
using Mono.CSharp;
using Unity.Netcode;
using UnityEngine;



public interface IGetLoadOut {
    public PlayerLoadout GetLoadOut();
}

public interface IGetTransform {
    public Transform GetTransform();
}

public interface IGetIInput {
    public IInput GetInput();
}

[Serializable]
public class AvaterSyncData3 : INetworkSerializable
{
    public Vector2 Pos;
    public Vector2 TargetVec;
    public Vector2 NowVec;
    public Vector2 AimPos;
    public Vector2 LastAimPos;
    public float Towards;
    public float RotVec;
    public float ClientUpdateTimeStamp;
    public float Power;
    public float ShootCd;
    
    public bool IsAim => AimPos != Vector2.zero;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Pos);
        serializer.SerializeValue(ref TargetVec);
        serializer.SerializeValue(ref NowVec);
        serializer.SerializeValue(ref AimPos);
        serializer.SerializeValue(ref LastAimPos);
        serializer.SerializeValue(ref Towards);
        serializer.SerializeValue(ref RotVec);
        serializer.SerializeValue(ref ClientUpdateTimeStamp);
        serializer.SerializeValue(ref Power);
        serializer.SerializeValue(ref ShootCd);
    }
}

public interface IAvaterSync : IGetLoadOut,IGetTransform,IGetIInput{
    public NetworkVariable<AvaterSyncData3> GetSyncData();
    public void AvaterDataSyncServerRpc(AvaterSyncData3 data);
    public bool IsOwner();
}

public class AvaterStateData2  {
    
    private IAvaterSync Avater;
    public AvaterSyncData3 Data{ get;private set; }

    private Transform RotCenter;

    public AvaterStateData2(IAvaterSync avater) {
        Avater = avater;
        Data = new AvaterSyncData3();
        RotCenter = Avater.GetTransform().Find("RotCenter");
    }
    
    public void DataSync() {
        if (Avater.IsOwner()) {
            Avater.AvaterDataSyncServerRpc(Data);
        }
    }

    public void ClientUpdate() {
        if (Avater.IsOwner()) {
            //--Input
            Data.TargetVec = Avater.GetInput().MoveJoy();
            Data.AimPos = Avater.GetInput().AimJoy();
            //--Input
            
            float missTime = Time.time - Data.ClientUpdateTimeStamp;
            Data.ClientUpdateTimeStamp = Time.time; //todo change to serverSyncTime

            //--Move
            Vector2 vec = Data.TargetVec - Data.NowVec;
            Vector2 direction = vec.normalized;
            Vector2 newVec = Data.TargetVec;
            float distance = vec.magnitude;
            float moveFriction = AvaterAttribute.MoveFriction;
            if(distance > moveFriction){
                newVec = Data.NowVec + direction * Mathf.Min(moveFriction, distance);
            }
            
            Data.NowVec = newVec;
            Data.Pos = Data.Pos + Data.NowVec * Avater.GetLoadOut().NowAttribute.MoveSpeed * missTime;
            //--Move
            
            //--Rot
            float targetTowards =  !Data.IsAim
                ? Data.TargetVec != Vector2.zero ? Data.TargetVec.Angle() : Data.Towards
                : Data.AimPos.Angle();
            Data.Towards = Mathf.SmoothDampAngle(Data.Towards, targetTowards, ref Data.RotVec, AvaterAttribute.RotSpeed, Mathf.Infinity, missTime);
            //--Rot
            
            //--Shoot
            var weapon = Avater.GetLoadOut().GetWeaponInfo();
            if (weapon != null && weapon.CanShoot(Data)) {
                Data.Towards = Data.AimPos.Angle();
                Data.RotVec = 0;
            } 
            //--Shoot
        } else {
            Data = Avater.GetSyncData().Value;
        }

        Avater.GetTransform().position = Data.Pos;
        RotCenter.eulerAngles = Vector3.forward*Data.Towards;
    }   
}
