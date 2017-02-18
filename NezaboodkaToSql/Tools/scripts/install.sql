/* ********************************************

	Create Nezaboodka administrative database
			and nezaboodka users

********************************************* */

CREATE DATABASE `nz_admin_db` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_BIN`;
USE `nz_admin_db`;


/* ********************************************

		Administrative database tables

*/

/*	Databases list to store user rights:
		0 - ReadWrite
		1 - ReadOnly
		2 - NoAccess
*/
CREATE TABLE `db_list`(
	`name` VARCHAR(64) PRIMARY KEY NOT NULL UNIQUE
		CHECK(`name` != ''),
	`access` TINYINT UNSIGNED NOT NULL DEFAULT 0	-- ReadWrite
		CHECK(`access` < 3),
	`is_removed` BOOLEAN NOT NULL DEFAULT FALSE
) ENGINE=`INNODB` COLLATE `UTF8_GENERAL_CI`;

/* ********************************************

			Stored procedures

*/

DELIMITER //
DROP PROCEDURE IF EXISTS QEXEC //
CREATE PROCEDURE QEXEC(
	IN query_text TEXT
)
BEGIN
	DECLARE is_prepared BOOLEAN DEFAULT FALSE;
	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SET @prep_str = NULL;
		IF is_prepared THEN
			DEALLOCATE PREPARE p_prep_proc;
		END IF;
		RESIGNAL;
	END;

	SET @qexec_row_count = 0;
	SET @prep_str = query_text;

	PREPARE p_prep_proc FROM @prep_str;
	SET is_prepared = TRUE;

	EXECUTE p_prep_proc;
	SET @qexec_row_count = ROW_COUNT();

	DEALLOCATE PREPARE p_prep_proc;
	SET @prep_str = NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _prepare_new_database //
CREATE PROCEDURE _prepare_new_database(
	IN db_name VARCHAR(64)
)
BEGIN
/*---------------------------------------/
			Schema Info
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- type
		"CREATE TABLE `", db_name, "`.`type`(
			`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
			`name` VARCHAR(128) NOT NULL UNIQUE
				CHECK(`name` != ''),
			`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`
				CHECK(`table_name` != ''),
			`base_type_name` VARCHAR(128)
				CHECK(`base_type_name` != '')
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- type_closure
		"CREATE TABLE `", db_name, "`.`type_closure`(
			`ancestor` INT NOT NULL,
			`descendant` INT NOT NULL,
			
			FOREIGN KEY(`ancestor`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
			
			FOREIGN KEY(`descendant`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
			
			CONSTRAINT `uc_keys`
				UNIQUE (`ancestor`, `descendant`)
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- field
		"CREATE TABLE `", db_name, "`.`field` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`owner_type_name` VARCHAR(128) NOT NULL
				CHECK(`owner_type_name` != ''),
			`owner_type_id` INT DEFAULT NULL,
			`name` VARCHAR(128) NOT NULL
				CHECK(`name` != ''),
			`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
				CHECK(`col_name` != ''),
			`type_name` VARCHAR(64) NOT NULL
				CHECK(`type_name` != ''),
			`ref_type_id` INT DEFAULT NULL,
			`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
			`compare_options` ENUM (
				'None',
				'IgnoreCase',
				'IgnoreNonSpace',
				'IgnoreSymbols',
				'IgnoreKanaType',
				'IgnoreWidth',
				'OrdinalIgnoreCase',
				'StringSort',
				'Ordinal'
			) NOT NULL DEFAULT 'None',
			`back_ref_name` VARCHAR(128) DEFAULT NULL
				CHECK(`back_ref_name` != ''),
			`back_ref_id` INT DEFAULT NULL,
			
			FOREIGN KEY(`owner_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
			
			FOREIGN KEY(`ref_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE RESTRICT,	-- to prevent deletion of referenced type
			
			FOREIGN KEY(`back_ref_id`)
				REFERENCES `field`(`id`)
				ON DELETE SET NULL,

			CONSTRAINT `uc_type_fields`
				UNIQUE (`owner_type_name`, `name`),

			CONSTRAINT `uc_table_columns`
				UNIQUE (`owner_type_name`, `col_name`)
		) ENGINE=`INNODB`;"
	));

/*---------------------------------------/
			Default Structure
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- db_key
		"CREATE TABLE `", db_name, "`.`db_key` (
			`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
			`real_type_id` INT NOT NULL,
			
			FOREIGN KEY (`real_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE	-- delete key when it's type info is removed
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- list
		"CREATE TABLE `", db_name, "`.`list` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`owner_id` BIGINT(0) NOT NULL,
			`type_id` INT NOT NULL,
			`field_id` INT NOT NULL,
			
			FOREIGN KEY (`owner_id`)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE,	-- cleanup list of deleted object
			
			FOREIGN KEY (`type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,	-- delete list when it's type info is removed
			
			FOREIGN KEY (`field_id`)
				REFERENCES `field`(`id`)
				ON DELETE CASCADE	-- delete list when it's field info is removed
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- list_item
		"CREATE TABLE `", db_name, "`.`list_item` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`list_id` INT NOT NULL,
			`ref` BIGINT(0) NOT NULL,
			
			FOREIGN KEY (`list_id`)
				REFERENCES `list`(`id`)
				ON DELETE CASCADE,	-- clear removed lists
			
			FOREIGN KEY (`ref`)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE,	-- remove deleted object from all lists

			CONSTRAINT `uc_list_ref`	-- no duplicates
				UNIQUE (`list_id`, `ref`)
		) ENGINE=`INNODB`;"
	));
END //


/*********************************************
	Effectively alter database list
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
		CALL _cleanup_temp_tables_after_alter_database_list();
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

	CALL _cleanup_temp_tables_after_alter_database_list();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _cleanup_temp_tables_after_alter_database_list //
CREATE PROCEDURE _cleanup_temp_tables_after_alter_database_list()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `db_rem_list`;
	DROP TEMPORARY TABLE IF EXISTS `db_add_list`;
END //


/*********************************************
		Alter database schema
*/
/*	Protocol for altering database schema:

	1. call `before_alter_database_schema`;
	2. fill `type_add_list`, `type_rem_list`, `field_add_list` and `field_rem_list` tables;
	3. call `alter_database_schema`.
*/

-- TODO: alter database schema procedures


/* ********************************************

		Create Nezaboodka users
			and grant rights for databases

********************************************* */

CREATE USER `nz_admin`@'%' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'%';

/*	Localhost user	*/
CREATE USER `nz_admin`@'localhost' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'localhost';

FLUSH PRIVILEGES;
