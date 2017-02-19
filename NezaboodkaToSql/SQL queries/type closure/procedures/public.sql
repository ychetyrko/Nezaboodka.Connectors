/*======================================

		Nezaboodka Admin database
			public procedures

----------------------------------------

	Protocol to alter database schema:

 1. Call `before_alter_database_schema`
 2. Fill following tables:
	- `fields_add_list`
	- `fields_rem_list`
	- `types_add_list`
	- `types_rem_list`
 3. Call `alter_database_schema`

======================================*/﻿

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_database_schema //
CREATE PROCEDURE before_alter_database_schema()
BEGIN
	CALL _before_alter_fields();
	CALL _before_alter_types();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS alter_database_schema //
CREATE PROCEDURE alter_database_schema(
	IN db_name varchar(64)
)
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		ROLLBACK;
		CALL _cleanup_temp_tables_after_alter_db_schema();
		SET @db_name = NULL;
		RESIGNAL;
	END;

	-- To move all queries that provide implicit commit to the end of execution
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`alter_query`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`alter_query`(
		`ord` INT NOT NULL UNIQUE AUTO_INCREMENT,
		`query_text` TEXT DEFAULT NULL
	) ENGINE=`INNODB`;

	SET @db_name = db_name;
/*
-- Debug
	SELECT @db_name AS 'Database name';
*/
	CALL _temp_before_common();
	CALL _temp_before_remove_fields();
	CALL _temp_before_remove_types();
	CALL _temp_before_add_types();
	CALL _temp_before_add_fields();

	START TRANSACTION;

	CALL _remove_fields();
	CALL _remove_types();
	CALL _add_types();
	CALL _add_fields();

	COMMIT;
/*
-- Debug
	SELECT * FROM `nz_test_closure`.`alter_query`;
*/
-- Apply all changes
	BEGIN
		DECLARE q_text TEXT DEFAULT NULL;
		
		DECLARE done BOOLEAN DEFAULT FALSE;
		DECLARE query_cur CURSOR FOR
			SELECT `query_text`
			FROM `nz_test_closure`.`alter_query`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET done = TRUE;

		OPEN query_cur;

		FETCH query_cur
		INTO q_text;
		WHILE NOT done DO
/*
-- Debug
			SELECT q_text;
*/
			CALL QEXEC(q_text);

			FETCH query_cur
			INTO q_text;
		END WHILE;

		CLOSE query_cur;
	END;

	CALL _cleanup_temp_tables_after_alter_db_schema();
	SET @db_name = NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _cleanup_temp_tables_after_alter_db_schema //
CREATE PROCEDURE _cleanup_temp_tables_after_alter_db_schema()
BEGIN
	CALL _temp_after_common();
	CALL _temp_after_remove_fields();
	CALL _temp_after_remove_types();
	CALL _temp_after_add_types();
	CALL _temp_after_add_fields();
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`alter_query`;
END //
