/*======================================

		Nezaboodka Admin database
			public procedures

----------------------------------------

	Protocol to alter database schema:

 1. Call `before_alter_db_schema`
 2. Fill following tables:
	- `fields_add_list`
	- `fields_rem_list`
	- `types_add_list`
	- `types_rem_list`
 3. Call `alter_db_schema`

======================================*/﻿

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_db_schema //
CREATE PROCEDURE before_alter_db_schema()
BEGIN
	CALL before_alter_fields();
	CALL before_alter_types();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS alter_db_schema //
CREATE PROCEDURE alter_db_schema()
BEGIN
	CALL remove_fields();
	CALL remove_types();

	CALL add_types();
	CALL add_fields();
END //
