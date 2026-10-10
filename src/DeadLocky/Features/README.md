# Features

PrepareGame owns directory selection, saved Steam sign-in and installation workflow.
LaunchGame owns runtime setup, game launch, monitoring and exit cleanup.
Each slice has Model/Api/Ui segments as needed. Controllers and implementation adapters remain internal;
public contracts and native view methods provide the boundary used by App and Pages.
Features never import peer features. Pages coordinates them using injected delegates.
