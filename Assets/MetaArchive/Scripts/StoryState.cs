public enum StoryState
{
    // 초기화
    Game_Start_Screen,
    Player_Name_Input,
    Intro_Meet_Dungddangi,

    // 마무리
    Ending_With_Dungddangi,
    Give_Gift_Prompt,
    Gift_Received,
    Final_Ending_Prompt,
    Game_Ended,

    Camera_Standby, // 카메라 켜기 UI 노출
    Camera_Scanning, // AR 추적 활성화, 이미지 스캔 대기
    Character_Intro, // 캐릭터 스폰+손흔들기 재생 중
    Dialogue_Running, // UI+대사 진행
    Flow_Completed // 종료 후 카메라 UI로 복귀
}
