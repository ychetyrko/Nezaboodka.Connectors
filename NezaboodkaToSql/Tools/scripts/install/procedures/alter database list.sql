/*********************************************
		Alter database list
*/
/*	Protocol for altering database list:

	1. call `before_alter_database_list` to create temporary tables if not created;
	2. fill `db_rem_list` and `db_add_list` tables with database names;
	3. call `alter_database_list`.
*/

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_database_list //
CREATE PROCEDURE before_alter_database_list()
BEGIN
-- List of databases to create
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`db_add_list`;
	CREATE TEMPORARY TABLE `nz_admin_db`.`db_add_list`(
		`name` VARCHAR(64) NOT NULL UNIQUE
	) ENGINE=`MEMORY` COLLATE `UTF8_GENERAL_CI`;

-- List of databases to drop
	DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`db_rem_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`db_rem_list`(
		`name` VARCHAR(64) NOT NULL UNIQUE
	) ENGINE=`MEMORY` COLLATE `UTF8_GENERAL_CI`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_databases //
CREATE PROCEDURE _remove_databases()
BEGIN
	UPDATE `db_list`
	SET `is_removed` = TRUE
	WHERE `name` IN (
		SELECT `name`
		FROM `db_rem_list`
	);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS cleanup_removed_databases //
CREATE PROCEDURE cleanup_removed_databases()
BEGIN
	DECLARE done INT DEFAULT FALSE;
	DECLARE db_name VARCHAR(64);
	DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_list`
		WHERE `is_removed`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	OPEN cur;
	FETCH cur INTO db_name;
	WHILE NOT done DO
		CALL QEXEC(CONCAT(
			"DROP DATABASE IF EXISTS ", db_name
		));
		FETCH cur INTO db_name;
	END WHILE;
	CLOSE cur;

	DELETE FROM `db_list`
	WHERE `is_removed`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_databases //
CREATE PROCEDURE _add_databases()
BEGIN
	INSERT INTO `db_list`
	(`name`)
	SELECT `name`
	FROM `db_add_list`; 
END //


DELIMITER //
DROP PROCEDURE IF EXISTS alter_database_list //
CREATE PROCEDURE alter_database_list()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		ROLLBACK;
		CALL _cleanup_temp_alter_database_list();
		RESIGNAL;
	END;

	START TRANSACTION;

	CALL _remove_databases();
	CALL _add_databases();
	
	COMMIT;

-- Create new databases
	BEGIN
		DECLARE done INT DEFAULT FALSE;
		DECLARE db_name VARCHAR(64);
		DECLARE cur CURSOR FOR
			SELECT `name`
			FROM `db_add_list`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET done = TRUE;

		OPEN cur;
		FETCH cur INTO db_name;
		WHILE NOT done DO
			CALL QEXEC(CONCAT(
				"CREATE DATABASE `", db_name, "`;"
			));
			CALL _prepare_new_database(db_name);
			FETCH cur INTO db_name;
		END WHILE;
		CLOSE cur;
	END;

	CALL _cleanup_temp_alter_database_list();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _cleanup_temp_alter_database_list //
CREATE PROCEDURE _cleanup_temp_alter_database_list()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `db_rem_list`;
	DROP TEMPORARY TABLE IF EXISTS `db_add_list`;
END //
